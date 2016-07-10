using System.Collections.Generic;

namespace WrappedSqlFileStream.Mapping
{
    public interface IMappingProvider
    {
        /// <summary>
        /// Returns a set of key-value pairs that map a set of properties to a set of columns, where the key is the property name and the value is the corresponding column name
        /// </summary>
        /// <returns></returns>
        Dictionary<string, string> GetPropertyMappings();

        /// <summary>
        /// Returns the property name that is mapped to the unique identifer column
        /// </summary>
        /// <returns></returns>
        string GetIdentifierName();

        /// <summary>
        /// Returns the property name that is mapped to the FILESTREAM column
        /// </summary>
        /// <returns></returns>
        string GetFileStreamName();

        /// <summary>
        /// Returns the property name that is mapped to the FILESTREAM column
        /// </summary>
        /// <returns></returns>
        string GetFileStreamProperty();

        /// <summary>
        /// Returns the name of the table that the entity will be mapped to
        /// </summary>
        /// <returns></returns>
        string GetTableName();
    }
}