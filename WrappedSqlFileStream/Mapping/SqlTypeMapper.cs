using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace WrappedSqlFileStream.Mapping
{
    public abstract class SqlTypeMapper
    {
        private static object _locker = new object();

        private static readonly Dictionary<Type, IEnumerable<PropertyInfo>> TypeLookup = new Dictionary<Type, IEnumerable<PropertyInfo>>();

        protected Dictionary<string, string> _mappings;

        protected SqlTypeMapper(Dictionary<string, string> mappings)
        {
            _mappings = mappings;
        }

        /// <summary>
        /// Returns the cached properties of a specified type
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        protected static IEnumerable<PropertyInfo> GetCachedProperties(Type type)
        {
            IEnumerable<PropertyInfo> properties;

            lock (_locker)
            {
                if (!TypeLookup.TryGetValue(type, out properties))
                {
                    properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public).ToList();
                    TypeLookup.Add(type, properties);
                }
            }

            return properties;
        }

        /// <summary>
        /// Returns the properties of a specified type
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<PropertyInfo> GetProperties(Type type)
        {
            return GetCachedProperties(type);
        }
    }
}