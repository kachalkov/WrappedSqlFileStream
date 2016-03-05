using System.Collections.Generic;

namespace WrappedSqlFileStream.Mapping
{
    public interface IMappingProvider
    {
        Dictionary<string, string> GetPropertyMappings();

        string GetIdentifierName();

        string GetTableName();
    }
}