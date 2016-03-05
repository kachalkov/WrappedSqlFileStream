using System;
using System.Linq.Expressions;
using System.Runtime.Serialization;

namespace WrappedSqlFileStream.Mapping
{
    /// <summary>
    /// Compiles a type instantiator. This is a lot faster than using Activator.CreateInstance
    /// To use simply call New&lt;T$gt;.Instance()
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public static class New<T>
    {
        public static readonly Func<T> Instance = Creator();

        static Func<T> Creator()
        {
            Type t = typeof(T);
            if (t == typeof(string))
                return Expression.Lambda<Func<T>>(Expression.Constant(string.Empty)).Compile();

            if (HasDefaultConstructor(t))
                return Expression.Lambda<Func<T>>(Expression.New(t)).Compile();

            return () => (T)FormatterServices.GetUninitializedObject(t);
        }

        public static bool HasDefaultConstructor(Type t)
        {
            return t.IsValueType || t.GetConstructor(Type.EmptyTypes) != null;
        }
    }
}