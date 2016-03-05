CREATE DATABASE WrappedSqlFileStreamSample 
ON
PRIMARY ( NAME = WrappedSqlFileStreamDb,
    FILENAME = 'c:\temp\wrappedSqlFileStreamSample.mdf'),
FILEGROUP FileStreamGroup1 CONTAINS FILESTREAM( NAME = SampleDb,
    FILENAME = 'c:\temp\wrappedSqlFileStream')
LOG ON  ( NAME = WrappedSqlFileStreamLogs,
    FILENAME = 'c:\temp\wrappedSqlFileStreamSample.ldf')
GO

CREATE TABLE WrappedSqlFileStreamSample.dbo.Files
(
	[Id] [uniqueidentifier] ROWGUIDCOL NOT NULL UNIQUE, 
	[FileName] VARCHAR(255),
	[File] VARBINARY(MAX) FILESTREAM NULL
)
GO
