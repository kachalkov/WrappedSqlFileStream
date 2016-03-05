@echo off
nuget pack WrappedSqlFileStream\WrappedSqlFileStream.csproj -Properties Configuration=Release -OutputDirectory "WrappedSqlFileStream\bin\Release"
nuget pack NHibernateMapping\WrappedSqlFileStream.Mapping.NHibernate.csproj -Properties Configuration=Release -OutputDirectory "NHibernateMapping\bin\Release"
pause
