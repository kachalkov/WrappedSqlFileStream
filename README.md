When working with **SqlFileStream** in your code, you have to create a few hard-coded SQL statements, 
one for insert, and one to access the FileStream. Then you need a lot of boring, boilderplate code to 
get the actual SqlFileStream. You also realize you need some kind of wrapper around the SqlFileStream to 
properly handle committing of the transaction when you need to pass the stream over WCF.

**WrappedSqlFileStream** uses Reflection and mapping information to do away with all the hard-coded SQL
statements needed to work with a SQL FileStream column as a .NET Stream.

It also uses a context object containing references to the **SqlConnection** and **SqlTransaction**
objects that were used when opening the SqlFileStream. The transaction will be committed and the
connection closed and disposed when the stream is disposed, making it safe and easy to use and manage.

#### Sample and Usage

For the sample, we will be using a context that creates a connection and transaction internally,
using the following constructor overload.

public WrappedSqlFileStreamContext(IMappingProvider mappingProvider, string connectionString)
You will need a SQL Server 2008 or higher database. Run the Setup.sql script from the Samples project
on your SQL database and modify the connection string in the App.config as necessary

##### Creating a new FileStream for writing

Three objects are needed to work with WrappedSqlFileStream, a mapping provider, a context
and the WrappedSqlFileStream iteself.

First we create an instance of an **IMappingProvider**. Here we will use the **DefaultMappingProvider**
which assumes class and property names correlate 1:1 with table and column names, and uses reflection
to generate mappings. We pass this and the connection string to the context constructor.

        var mappingProvider = new DefaultMappingProvider<Files, Guid>("dbo", x => x.Id);
        var context = new WrappedSqlFileStreamContext<Files>(mappingProvider, connectionstring);

To create a new FileStream, a container row must first be created on the table. to create the row
we use the context object method **CreateRow**. The first parameter takes an expression that identifies
the byte array property that is mapped to the FileStream column.  The second parameter should be an object
instantiation expression, containing only the properties you want to include in the insert statement that
creates the new row.

        context.CreateRow(files => files.File, () => new Files()
        {
            Id = id,
            FileName = filename
        });

Internally, an INSERT statement is generated that sets all the defined properties to the assigned values
and sets the FileStream to 0x (0 bytes).

Create a WrappedSqlFileStream passing in the contest, the FileStream property and an expression identifying the row
(this expression is converted to a SQL WHERE clause using a custom expression walker and the mapping context,
so only simple AND and OR expressions are supported).  Specify the FileAccess mode as Write, then write to the stream
as you would any other stream.  Finally, commit the transaction and dispose of the connection.

        using (var sfs = new WrappedSqlFileStream<Files>(context, files => files.File, files => files.Id == id, FileAccess.Write))
        {
            file.CopyTo(sfs);
        }

##### Opening an existing FileStream for reading/writing

Create the mapping provider and the context.

        var mappingProvider = new DefaultMappingProvider<Files, Guid>("dbo", x => x.Id);
        var context = new WrappedSqlFileStreamContext<Files>(mappingProvider, connectionstring);

And create a WrappedSqlFileStream with FileAccess.Read mode.

        var stream = new WrappedSqlFileStream<Files>(context, files => files.File, files => files.Id == id, FileAccess.Read);

This stream can be cast to type Stream and returned from a WCF API, and when the stream is disposed where it is consumed,
the WrappedSqlFileStream in the WCF will be disposed of as well, triggering the transaction commit and connection close.

##### Retrieving associated columns

The WrappedSqlFileStream object has a **RowData** property that is of type T. When creating a WrappedFileStream with FileAccess.Read,
The RowData object will be populated with data from columns using the supplied mapping in the context. 

In the previous example, stream.RowData would be of type **Files** and would contain the values of the other fields in the row. 
This allows you to retrieve both the stream and the fields in one query. 
    
The stream also has an **OnDispose** delegate property, which allows you to trigger an action when the stream is disposed, 
but before the context is commited.

#### Reusing ORM Mappings

If we use NHibernate, we can leverage the ORM’s mapping to get the table and field mappings. Create an instance of an **NHibernateMappingProvider** and pass the **ISessionFactory**.

        var mappingProvider = new NHibernateMappingProvider<Files>(_sessionFactory);

It is important to note that NHibernate is only used for the mapping. The actual query is still run in a separate transaction.

An EntityFramework mapping provider is not yet implemented.

#### Using an external SqlConnection and SqlTransaction

If you find the need to use an existing connection and transaction, use the WrappedSqlFileStreamContext constructor that accepts those parameters.

        public WrappedSqlFileStreamContext(IMappingProvider mappingProvider, SqlConnection connection, SqlTransaction transaction)

In this case, committing of the transaction and disposing of the connection will not occur when the stream is disposed, allowing you use the 
WrappedSqlFileStream in a using block and still perform operations on the transaction before committing.

#### License

WrappedSqlFileStream is available under the MIT License

#### API Documentation

(To do)

* IMappingProvider
* WrappedSqlFileStreamContext
* WrappedSqlFileStream