pushd .
cd WrappedSqlFileStream
dotnet build --configuration Release 
dotnet pack --configuration Release --output Package
popd
pushd .
cd WrappedSqlFileStream.Mapping.NHibernate
dotnet build --configuration Release 
dotnet pack --configuration Release --output Package
popd

