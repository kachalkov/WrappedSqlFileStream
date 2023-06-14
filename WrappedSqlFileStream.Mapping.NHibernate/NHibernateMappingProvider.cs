using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using NHibernate;

namespace WrappedSqlFileStream.Mapping.NHibernate
{
    public class NHibernateMappingProvider<T> : IMappingProvider
    {
        private readonly ISessionFactory _sessionFactory;
        public string FileStream { get; }
        public Dictionary<string, string> PropertyMappings { get; }

        public NHibernateMappingProvider(ISessionFactory sessionFactory, Expression<Func<T, byte[]>> fileStreamFieldExpression)
        {
            _sessionFactory = sessionFactory;
            FileStream = ((MemberExpression)fileStreamFieldExpression.Body).Member.Name;
            PropertyMappings = _sessionFactory.GetPropertyMappings<T>();
        }

        public Dictionary<string, string> GetPropertyMappings()
        {
            return PropertyMappings;
        }

        public string GetIdentifierName()
        {
            return _sessionFactory.GetIdentifierName<T>();
        }

        public string GetFileStreamName()
        {
            return PropertyMappings[FileStream];
        }

        public string GetFileStreamProperty()
        {
            return FileStream;
        }
        
        public string GetTableName()
        {
            return _sessionFactory.GetTableName<T>();
        }
    }
}