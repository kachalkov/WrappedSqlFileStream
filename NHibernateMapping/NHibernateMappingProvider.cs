using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using NHibernate;

namespace WrappedSqlFileStream.Mapping.NHibernate
{
    public class NHibernateMappingProvider<T> : IMappingProvider
    {
        private readonly ISessionFactory _sessionFactory;
        private string _fileStream;
        private Dictionary<string, string> _propertyMappings;

        public NHibernateMappingProvider(ISessionFactory sessionFactory, Expression<Func<T, byte[]>> fileStreamFieldExpression)
        {
            _sessionFactory = sessionFactory;
            _fileStream = ((MemberExpression)fileStreamFieldExpression.Body).Member.Name;
            _propertyMappings = _sessionFactory.GetPropertyMappings<T>();
        }

        public Dictionary<string, string> GetPropertyMappings()
        {
            return _propertyMappings;
        }

        public string GetIdentifierName()
        {
            return _sessionFactory.GetIdentifierName<T>();
        }

        public string GetFileStreamName()
        {
            return _propertyMappings[_fileStream];
        }

        public string GetFileStreamProperty()
        {
            return _fileStream;
        }
        
        public string GetTableName()
        {
            return _sessionFactory.GetTableName<T>();
        }
    }
}