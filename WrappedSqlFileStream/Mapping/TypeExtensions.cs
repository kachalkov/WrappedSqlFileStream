using System;

namespace WrappedSqlFileStream.Mapping
{
    public static class TypeExtensions
    {
        public static Boolean IsNullableType(this Type typeToCheck)
        {
            return typeToCheck.IsGenericType &&
                   typeToCheck.GetGenericTypeDefinition() == typeof(Nullable<>);
        }
    }
}