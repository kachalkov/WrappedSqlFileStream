using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Reflection;

namespace WrappedSqlFileStream.Mapping
{
    /// <summary>
    /// Simple class to read data from a SqlDataReader into an instance of a type using a property to fieldname mapping
    /// </summary>
    public class SqlDataMapper<T> : SqlTypeMapper
    {
        public SqlDataMapper(Dictionary<string, string> mappings) : base(mappings)
        {
        }

        private static object GetValueFromType(Type type, SqlDataReader sqlDataReader, int ordinal)
        {
            object value = null;
            if (type == typeof(int))
            {
                value = sqlDataReader.GetInt32(ordinal);
            }
            else if (type == typeof(byte))
            {
                value = sqlDataReader.GetByte(ordinal);
            }
            else if (type == typeof(short))
            {
                value = sqlDataReader.GetInt16(ordinal);
            }
            else if (type == typeof(long))
            {
                value = sqlDataReader.GetInt64(ordinal);
            }
            else if (type == typeof(bool))
            {
                value = sqlDataReader.GetBoolean(ordinal);
            }
            else if (type == typeof(string))
            {
                value = sqlDataReader.GetString(ordinal);
            }
            else if (type == typeof(decimal))
            {
                value = sqlDataReader.GetDecimal(ordinal);
            }
            else if (type == typeof(double))
            {
                value = sqlDataReader.GetDouble(ordinal);
            }
            else if (type == typeof(float))
            {
                value = sqlDataReader.GetFloat(ordinal);
            }
            else if (type == typeof(DateTime))
            {
                value = sqlDataReader.GetDateTime(ordinal);
            }
            else if (type == typeof(Guid))
            {
                value = sqlDataReader.GetGuid(ordinal);
            }
            return value;
        }

        private static object GetValue(PropertyInfo property, SqlDataReader sqlDataReader, string fieldName)
        {
            fieldName = fieldName.StartsWith("[") && fieldName.EndsWith("]") ? fieldName.Substring(1, fieldName.Length - 2) : fieldName;
            var ordinal = sqlDataReader.GetOrdinal(fieldName);
            Type propertyType = property.PropertyType;

            if (property.PropertyType.IsEnum)
            {
                propertyType = property.PropertyType.GetEnumUnderlyingType();
            }
            else if (property.PropertyType.IsNullableType())
            {
                if (sqlDataReader.IsDBNull(ordinal))
                {
                    return null;
                }
                propertyType = Nullable.GetUnderlyingType(property.PropertyType);
            }
            return GetValueFromType(propertyType, sqlDataReader, ordinal);
        }

        /// <summary>
        /// Creates a new instance of T and maps the data in the dataReader using the specified property name to field name mapping
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sqlDataReader"></param>
        /// <returns></returns>
        public T Map(SqlDataReader sqlDataReader)
        {
            var result = New<T>.Instance();

            foreach (var property in GetCachedProperties(typeof(T)))
            {
                string fieldName;

                if (_mappings.TryGetValue(property.Name, out fieldName))
                {
                    if (fieldName.StartsWith("["))
                    {
                        fieldName = fieldName.Substring(1, fieldName.Length - 2);
                    }
                    if (sqlDataReader.HasColumn(fieldName))
                    {
#if NET40
                        property.SetValue(result, GetValue(property, sqlDataReader, fieldName), null);
#else
                        property.SetValue(result, GetValue(property, sqlDataReader, fieldName));
#endif
                    }
                }
            }

            return result;
        }


    }
}