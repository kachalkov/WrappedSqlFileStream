SQL Server's [FILESTREAM](https://technet.microsoft.com/en-us/library/bb933993(v=sql.105).aspx) is a very efficient way to store and access large binary data which works by storing the data in the file system instead of part of a table. .NET provides the [SqlFileStream](https://msdn.microsoft.com/en-us/library/system.data.sqltypes.sqlfilestream(v=vs.110).aspx) class to expose the FILESTREAM as a Stream object, allowing you to read from or write to the stream in chunks, and perform seeks as any regular Stream, but using SqlFileStream can be very complex and time consuming.

You have to create a few hard-coded SQL statements, one to insert a new row, and another to actually access the FileStream. Then you need a lot of ugly boilerplate code to get the actual SqlFileStream.

No ORM provider has a way to access a file stream and associated columns in a table as a typed object.

**WrappedSqlFileStream** uses Reflection with provided mapping information to do away with all the hard-coded SQL and boiler plate code, and provides the necessary wrapper to pass the stream over WCF while mapping the columns in the table to a .NET class.

You need to call Commit() on the SqlTransaction used when creating the SqlFileStream when disposing it.

One of the challenges of using SqlFileStream is finding a way to commit the transaction when you need to pass the stream over WCF.

WrappedSqlFileStream does this by keeping a reference to the SqlTransaction and overriding the Dispose() method, allowing you to call Commit() when the .NET framework disposes of the stream when the WCF connection is closed.

## Example and Usage

You will need a SQL Server 2008 or higher database. Run the Setup.sql script from the Samples project on your SQL database and modify the connection string in the App.config as necessary

Three objects are needed to work with WrappedSqlFileStream, an IMappingProvider, a WrappedSqlFileStreamContext and the WrappedSqlFileStream itself.

### Mapping a Table to a Type

In our sample, we have the table defined as:

	CREATE TABLE WrappedSqlFileStreamSample.dbo.Files
	(
		[Id] [uniqueidentifier] ROWGUIDCOL NOT NULL UNIQUE,
		[FileName] VARCHAR(255),
		[File] VARBINARY(MAX) FILESTREAM NULL
	)

And the class to be mapped. (The properties do not have to be virtual as the framework does not construct a proxy class)

	    public class Files
	    {
	        public virtual Guid Id { get; set; }
	        public virtual string FileName { get; set; }
	        public virtual byte[] File { get; set; }
	    }

To create an IMappingProvider we will use the **DefaultMappingProvider<T, TIdent>** class which generates mapping on the assumption that class and property names are the same as table and column names.  The constructor has the following signature:

    DefaultMappingProvider(string schema, Expression<Func<T, TIdent>> identifier);

The generic type parameters T and TIdent correspond to the type we are mapping to, and the type of the property that corresponds to the table's identity column.

The first parameter of the constructor is the schema name of the table, and the second parameter is an expression that returns the property that corresponds to the table's identity column.

	        var mappingProvider = new DefaultMappingProvider<Files, Guid>("dbo", x => x.Id);

### Creating a Context

The WrappedSqlFileStreamContext class contains the references to the SqlConnection and SqlTransaction which will be used to create the SqlFileStream, and which must be closed when the SqlFileStream is disposed.

There are two ways of using the context. One, which we will use in this example, creates the connection and transaction internally. The other allows you to use externally-provided transaction and connection objects.

We pass the IMappingProvider object and a connection string to our context constructor:

	        var context = new WrappedSqlFileStreamContext<Files>(mappingProvider, connectionstring);

### Creating a new FILESTREAM for writing

To create a new FILESTREAM, a row must first be created on the table. To do this we use the WrappedSqlFileStreamContext's **CreateRow** method.

	public void CreateRow(Expression<Func<T, byte[]>> fileStreamFieldExpression, Expression<Func<T>> newObjectExpression)

The first parameter takes an expression that identifies the byte array property that is mapped to the FILESTREAM column. This byte array will not contain the actual FILESTREAM data, it is only used to tell SqlFileStream which column stores the FILESTREAM. In the context of CreateRow, it tells the framework which column will be set to zero bytes when creating the new row.

The second parameter should be an object instantiation expression, containing only the properties you want to include in the insert statement that creates the new row.

	        context.CreateRow(files => files.File,
	        () => new Files()
	        {
	            Id = id,
	            FileName = filename
	        });

This statement tells the framework to create a new row on the table mapped to the Files type as specified by our IMappingProvider, and set the values for the columns mapped to the properties Id and FileName only. Additionally the column mapped to the File property is set to zero bytes.

The INSERT statement generated by this statement would be:

	INSERT INTO dbo.Files (File, Id, FileName) VALUES (0x, @Id, @FileName)

The mapped property values are parameterized, avoiding any SQL injection vulnerabilities.

This statement is executed immediately.  Now that the row exists in the table, we can use SqlFileStream to access the FILESTREAM.

### Instantiating the Stream and writing data

Create a WrappedSqlFileStream<T> with type parameter Files and the following parameters:

* our WrappedSqlFileStreamContext
* the FILESTREAM property
* an expression identifying the row
* the FileAccess mode FileAccess.Write

	new WrappedSqlFileStream<Files>(context, files => files.File, files => files.Id == id, FileAccess.Write)

The framework will create the following SQL command:

	SELECT File.PathName(), GET_FILESTREAM_TRANSACTION_CONTEXT() Id, FileName
	FROM dbo.Files WHERE Id = @id;

The first two fields returned by this query will be used to instantiate a SqlFileStream.

The WHERE clause is generated from the expression identifying the row using a custom expression walker and the mapping stored in the context.  The expression walker is very basic and supports only simple expressions.

The created WrappedSqlFileStream can now be accessed as any other Stream object. Here we use CopyTo to write a stream to our WrappedSqlFileStream.

	        using (var sfs = new WrappedSqlFileStream<Files>(context, files => files.File, files => files.Id == id, FileAccess.Write))
	        {
	            file.CopyTo(sfs);
	        }

Remember to put the stream in a using statement, or call Dispose when you are done. This will dispose the underlying SqlFileStream and call Commit on the transaction and Dispose on the connection.

#### Opening an existing FileStream for reading/writing

The steps for opening an existing FILESTREAM are much simpler than creating one. The mapping can be reused, instantiating it only once either in the constructor, or creating it once in the application lifetime and injecting it in. Create a WrappedSqlFileStream but pass FileAccess.Read as the FileAccess mode.

	        var stream = new WrappedSqlFileStream<Files>(context, files => files.File, files => files.Id == id, FileAccess.Read);

This stream can be sent over WCF, and when the stream is disposed where it is consumed, the WrappedSqlFileStream will be disposed of as well, triggering the transaction commit and connection close.

#### Retrieving associated columns

The WrappedSqlFileStream object has a **RowData** property that is of type T. When creating a WrappedFileStream with FileAccess.Read, the RowData object will be populated with data from columns using the supplied mapping in the context.

In the previous example, stream.RowData would be of type **Files** and would contain the values of the other fields in the row. This allows you to retrieve both the stream and the fields in one query.

The stream also has an **OnDispose** delegate property, which allows you to trigger an action when the stream is disposed, but before the context is commited.

### Reusing ORM Mappings

If we use NHibernate, we can leverage the ORM’s mapping to get the table and field mappings. Create an instance of an **NHibernateMappingProvider** and pass the **ISessionFactory**.

	        var mappingProvider = new NHibernateMappingProvider<Files>(_sessionFactory);

It is important to note that NHibernate is only used for the mapping. The actual query is still run in a separate transaction.

An EntityFramework mapping provider is not yet implemented.

### Using an external SqlConnection and SqlTransaction

If you find the need to use an existing connection and transaction, use the WrappedSqlFileStreamContext constructor that accepts those parameters.

	        public WrappedSqlFileStreamContext(IMappingProvider mappingProvider, SqlConnection connection, SqlTransaction transaction)

In this case, committing of the transaction and disposing of the connection will not occur when the stream is disposed, allowing you use the WrappedSqlFileStream in a using block and still perform operations on the transaction before committing.

### License

WrappedSqlFileStream is available under the MIT License.

### API Documentation

(To do)

* IMappingProvider
* WrappedSqlFileStreamContext
* WrappedSqlFileStream

