using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace WrappedSqlFileStream.Mapping
{
    /// <summary>
    /// Implements a MappingProvider where the type name is mapped to the table of the same name, 
    /// and each public intance property of the specified type T is mapped to a column with the same name. 
    /// The mapping of the identifier column must be specified in the constructor
    /// </summary>
    /// <typeparam name="T">The type that will be used to create the mapping</typeparam>
    public class DefaultMappingProvider<T> : IMappingProvider
    {
        private string _identifier;
        private string _fileStream;
        private string _schema;
        private static IEnumerable<PropertyInfo> _typeProperties;
        protected Dictionary<string, string> _propertyMappings;

        private static IEnumerable<PropertyInfo> GetProperties()
        {
            if (_typeProperties == null)
            {
                _typeProperties = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public).ToList();
            }
            return _typeProperties;
        }

        public void SetIdentifierColumn<TIdent>(Expression<Func<T, TIdent>> identifierFieldExpression)
        {
            _identifier = ((MemberExpression)identifierFieldExpression.Body).Member.Name;
        }

        /// <summary>
        /// Creates an instance of the default mapping provider with a default schema of "dbo"
        /// </summary>
        /// <param name="fileStreamFieldExpression"></param>
        public DefaultMappingProvider(Expression<Func<T, byte[]>> fileStreamFieldExpression) : this("dbo", fileStreamFieldExpression)
        {
        }

        /// <summary>
        /// Creates an instance of the default mapping provider with the provided schema name
        /// </summary>
        /// <param name="schema"></param>
        /// <param name="fileStreamFieldExpression"></param>
        public DefaultMappingProvider(string schema, Expression<Func<T, byte[]>> fileStreamFieldExpression)
        {
            _schema = schema;
            _fileStream = ((MemberExpression)fileStreamFieldExpression.Body).Member.Name;
            var properties = GetProperties();
            _propertyMappings = properties.Select(x => new { Key = x.Name, Value = "[" + x.Name + "]" }).ToDictionary(x => x.Key, x => x.Value);
        }

        /// <summary>
        /// Returns a dictionary containing the mapping of property names to column names where the column name is equal to the property name
        /// enclosed with square brackets
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, string> GetPropertyMappings()
        {
            return _propertyMappings;
        }

        public string GetFileStreamName()
        {
            return _propertyMappings[_fileStream];
        }

        public string GetFileStreamProperty()
        {
            return _fileStream;
        }

        /// <summary>
        /// Returns the name of the property specified in the constructor
        /// </summary>
        /// <returns></returns>
        public string GetIdentifierName()
        {
            return _identifier == null ? null : _propertyMappings[_identifier];
        }

        /// <summary>
        /// Returns the schema concatenated to the type name
        /// </summary>
        /// <returns></returns>
        public string GetTableName()
        {
            return _schema + "." + typeof(T).Name;
        }
    }
}
