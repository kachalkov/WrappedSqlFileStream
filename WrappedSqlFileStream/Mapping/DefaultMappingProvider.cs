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
    /// <typeparam name="TIdent">The type of the property to be mapped to the identifier</typeparam>
    public class DefaultMappingProvider<T, TIdent> : IMappingProvider
    {
        private string _identifier;
        private string _schema;
        private static IEnumerable<PropertyInfo> _typeProperties;

        private static IEnumerable<PropertyInfo> GetProperties()
        {
            if (_typeProperties == null)
            {
                _typeProperties = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public).ToList();
            }
            return _typeProperties;
        }

        /// <summary>
        /// Creates an instance of the mapping provider with a default schema of "dbo"
        /// </summary>
        /// <param name="identifier"></param>
        public DefaultMappingProvider(Expression<Func<T, TIdent>> identifier)
        {
            _schema = "dbo";
            _identifier = ((MemberExpression)identifier.Body).Member.Name;
        }

        /// <summary>
        /// Creates an instance of the mapping provider
        /// </summary>
        /// <param name="schema"></param>
        /// <param name="identifier"></param>
        public DefaultMappingProvider(string schema, Expression<Func<T, TIdent>> identifier)
        {
            _schema = schema;
            _identifier = ((MemberExpression)identifier.Body).Member.Name;
        }

        /// <summary>
        /// Returns a dictionary containing the mapping of property names to column names where the column name is equal to the property name
        /// enclosed with square brackets
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, string> GetPropertyMappings()
        {
            IEnumerable<PropertyInfo> properties = GetProperties();
            return properties.Select(x => new {Key = x.Name, Value = "[" + x.Name + "]"}).ToDictionary(x => x.Key, x => x.Value);
        }

        /// <summary>
        /// Returns the name of the property specified in the constructor
        /// </summary>
        /// <returns></returns>
        public string GetIdentifierName()
        {
            return _identifier;
        }

        /// <summary>
        /// Returns the schema concatenated to the type name
        /// </summary>
        /// <returns></returns>
        public string GetTableName()
        {
            return _schema + "." + typeof (T).Name;
        }
    }
}
