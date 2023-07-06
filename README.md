[![Latest version](https://img.shields.io/badge/nuget-v2.0.0-blue)](https://www.nuget.org/packages/WrappedSqlFileStream)


**WrappedSqlFileStream** is a library that helps remove boilerplate code around using a SQL FILESTREAM and the .NET SqlFileStream API to provide typed access to the table row containing the FILESTREAM and any other columns associated with it.

To avoid confusion in this document, FILESTREAM refers to a SQL FILESTREAM. SqlFileStream is the C# class used to access a FILESTREAM. A FileStream is a regular C# FileStream.

# What is a FILESTREAM

SQL Server's [FILESTREAM](https://technet.microsoft.com/en-us/library/bb933993(v=sql.105).aspx) is a way to "store" and associate large binary data with a row in a database.  It does so by storing the data in the file system instead of part of a table. SQL Server then allows you to access the underlying file as a Stream, so you don't have to load it into a byte array which would consume memory, but read it as a regular FileStream.

.NET provides the [SqlFileStream](https://msdn.microsoft.com/en-us/library/system.data.sqltypes.sqlfilestream(v=vs.110).aspx) class to expose the FILESTREAM as a Stream object, allowing you to read from or write to the stream in chunks, and perform seeks as any regular Stream, but using SqlFileStream can be very complex and time consuming.

You have to create a few hard-coded SQL statements, one to insert a new row, and another to actually access the FileStream. Then you need a lot of ugly boilerplate code to get the actual SqlFileStream.

# Wrapping the SqlFileStream

**WrappedSqlFileStream** uses reflection with provided mapping information to do away with all the hard-coded SQL and boiler plate code, and provides the necessary wrapper to pass the stream over WCF while mapping the columns in the table to a .NET class.

One caveat when using `SqlFileStream` is that you need to call `Commit()` on the `SqlTransaction` used when creating the `SqlFileStream` when disposing it.  This can pose a problem when passing the stream directly to an API endpoint or WCF.  THe .NET Framework will call `Dispose()` on any `IDisposable` it returns, however this will not normally call `Commit()` on the `SqlTransaction`.

WrappedSqlFileStream solves this issue by keeping a reference to the `SqlTransaction` and overriding the `Dispose()` method, allowing you to call `Commit()` when the .NET framework disposes of the stream when the API endpoint or WCF connection is closed.

## Example and Usage

You will need a SQL Server 2008 or higher database. You will also need to enable FILESTREAM.

https://learn.microsoft.com/en-us/sql/relational-databases/blob/enable-and-configure-filestream?view=sql-server-ver16

To run the Sample project, make sure you have enabled FILESTREAM, then run the `Setup.sql` script from the Samples project on your SQL database. Modify the connection string in the `App.config` as necessary.

## Mapping a Table to a Type

In our sample, we have the table defined as:

```sql
CREATE TABLE WrappedSqlFileStreamSample.dbo.Files
(
	[Id] [uniqueidentifier] ROWGUIDCOL NOT NULL UNIQUE,
	[FileName] VARCHAR(255),
	[File] VARBINARY(MAX) FILESTREAM NULL
)
```

And the class to be mapped. (The properties do not have to be virtual as the framework does not construct a proxy class)

```cs
public class Files
{
	public virtual Guid Id { get; set; }
	public virtual string FileName { get; set; }
	public virtual byte[] File { get; set; }
}
```

To create an `IMappingProvider` we will use the `DefaultMappingProvider<T, TIdent>` class which generates mapping on the assumption that class and property names are the same as table and column names.  The constructor has the following signature:

```cs
DefaultMappingProvider(string schema, Expression<Func<T, TIdent>> identifier);
```

The generic type parameters `T` and `TIdent` correspond to the type we are mapping to that represents the table, and the type of the property that is mapped to the table's identity column.

The first parameter of the constructor is the schema name of the table, and the second parameter is an expression that returns the property that corresponds to the table's identity column.

```cs
var mappingProvider = new DefaultMappingProvider<Files, Guid>("dbo", x => x.Id);
```

### Creating a Context

The `WrappedSqlFileStreamContext` class contains the references to the `SqlConnection` and `SqlTransaction` which will be used to create the `SqlFileStream`, and which must be closed when the `SqlFileStream` is disposed.

There are two ways of using the context. One, which we will use in this example, creates the connection and transaction internally. The other allows you to use externally-provided transaction and connection objects.

We pass the `IMappingProvider` object and a connection string to our context constructor:

```cs
var context = new WrappedSqlFileStreamContext<Files>(mappingProvider, connectionstring);
```

## Creating a new FILESTREAM for writing

To create a new `FILESTREAM`, a row must first be created on the table. To do this we use the `WrappedSqlFileStreamContext`'s `CreateRow()` method.

```cs
public void CreateRow(Expression<Func<T, byte[]>> fileStreamFieldExpression, Expression<Func<T>> newObjectExpression)
```

The first parameter takes an expression that identifies the byte array property that is mapped to the FILESTREAM column. This byte array will not contain the actual FILESTREAM data, it is only used to tell SqlFileStream which column stores the FILESTREAM. In the context of CreateRow, it tells the library which column will be set to zero bytes when creating the new row.

The second parameter should be an object instantiation expression, containing only the properties you want to include in the insert statement that creates the new row.

```cs
context.CreateRow(files => files.File,
() => new Files()
{
	Id = id,
	FileName = filename
});
```

This statement tells the library to create a new row on the table mapped to the `Files` type as specified by our `IMappingProvider`, and sets the values for the columns mapped to the properties `Id` and `FileName` only. Additionally the column mapped to the `File` property is set to zero bytes.

The SQL statement generated by this code would be:

```sql
INSERT INTO dbo.Files (File, Id, FileName) VALUES (0x, @Id, @FileName)
```

The mapped property values are parameterized, avoiding any SQL injection vulnerabilities.

This statement is executed immediately.  Now that the row exists in the table, we can use a `SqlFileStream` to access the `FILESTREAM`.

## Instantiating the Stream and writing data

Create a `WrappedSqlFileStream<T>` with type parameter `Files` and the following parameters:

* our `WrappedSqlFileStreamContext`
* the `FILESTREAM` property
* an expression identifying the row
* the `FileAccess` mode `FileAccess.Write`

```cs
new WrappedSqlFileStream<Files>(context, files => files.File, files => files.Id == id, FileAccess.Write)
```

This will generate the following SQL:

```sql
SELECT File.PathName(), GET_FILESTREAM_TRANSACTION_CONTEXT() Id, FileName
FROM dbo.Files WHERE Id = @id;
```

The first two fields returned by this query will be used to instantiate a `SqlFileStream`.

The `WHERE` clause is generated from the expression identifying the row using a custom expression walker and the mapping stored in the context.  The expression walker is very basic and supports only simple expressions.

The created `WrappedSqlFileStream` can now be accessed as any other `Stream` object. Here we use `CopyTo` to write a filestream to our `WrappedSqlFileStream`.

```cs
using (var fs = new FileStream("foo.pdf", FileMode.Open, FileAccess.Read))
using (var sfs = new WrappedSqlFileStream<Files>(context, files => files.File, files => files.Id == id, FileAccess.Write))
{
	fs.CopyTo(sfs);
}
```

Remember to put the stream in a `using` statement, or call `Dispose` when you are done. This will dispose the underlying `SqlFileStream` and call `Commit()` on the `SqlTransaction` and `Dispose()` on the `SqlConnection`.

## Opening an existing FILESTREAM for reading/writing

The steps for opening an existing `FILESTREAM` are much simpler than creating one. The mapping can be reused, instantiating it only once either in the constructor, or creating it once in the application lifetime and injecting it in. Create a `WrappedSqlFileStream` but pass `FileAccess.Read` as the `FileAccess` mode.

```cs
var stream = new WrappedSqlFileStream<Files>(context, files => files.File, files => files.Id == id, FileAccess.Read);
```

This stream can be sent over an API endpoint or WCF, and when the stream is disposed where it is consumed, the `WrappedSqlFileStream` will be disposed of as well, triggering the transaction commit and connection close.

The stream also has an `OnDispose()` property which allows you to call your own code when the stream is disposed, but before the context is commited.

## Retrieving associated columns

The `WrappedSqlFileStream<T>` object has a `RowData` property that is of type `T`. When creating a `WrappedFileStream` with `FileAccess.Read`, the `RowData` object will be populated with data from columns using the supplied mapping in the context.

In the previous example, `stream.RowData` would be of type `Files` and would contain the values of the other fields in the row. This allows you to retrieve both the stream and the fields in one query.

# Reusing ORM Mappings

If you use NHibernate, you can leverage the ORM’s mapping to get the table and field mappings. 

Create an instance of an **NHibernateMappingProvider** and pass the **ISessionFactory**.

```cs
var mappingProvider = new NHibernateMappingProvider<Files>(_sessionFactory);
```

It is important to note that NHibernate is only used for the mapping. The actual query is still run in a separate transaction.

An EntityFramework mapping provider is not yet implemented.

# Using an external SqlConnection and SqlTransaction

If you find the need to use an existing connection and transaction, use the WrappedSqlFileStreamContext constructor that accepts those parameters.

```cs
public WrappedSqlFileStreamContext(IMappingProvider mappingProvider, SqlConnection connection, SqlTransaction transaction)
```

In this case, committing of the transaction and disposing of the connection will *not* occur when the stream is disposed, allowing you use the `WrappedSqlFileStream` in a using block and still perform operations on the transaction before committing.

# License

WrappedSqlFileStream is available under the MIT License.

# Troubleshooting

## FILESTREAM feature is disabled

You get the error:

```
FILESTREAM feature is disabled.
```

Make sure you enable FILESTREAM in SQL Server Configuration Manager, and restart the SQL Server service.

https://learn.microsoft.com/en-us/sql/relational-databases/blob/enable-and-configure-filestream?view=sql-server-ver16

* In the SQL Server Configuration Manager snap-in, locate the instance of SQL Server on which you want to enable FILESTREAM.
* Right-click the instance, and then click Properties.
* In the SQL Server Properties dialog box, click the FILESTREAM tab.
* Select the Enable FILESTREAM for Transact-SQL access check box.

If you can't find SQL Server Configuration Manager, try running the following in the Run dialog

```
sqlservermanager15.msc
```

## FILESTREAM feature doesn't have file system access enabled

You get the error:

```
Microsoft.Data.SqlClient.SqlException: 'FILESTREAM feature doesn't have file system access enabled.'
```

Execute the following SQL statements:

```
sp_configure 'filestream access level', 2

RECONFIGURE WITH OVERRIDE
```