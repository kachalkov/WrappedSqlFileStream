using System.Collections.Generic;
using NHibernate;

namespace WrappedSqlFileStream.Mapping.NHibernate
{
    public class NHibernateMappingProvider<T> : IMappingProvider
    {
        private readonly ISessionFactory _sessionFactory;

        public NHibernateMappingProvider(ISessionFactory sessionFactory)
        {
            _sessionFactory = sessionFactory;
        }

        public Dictionary<string, string> GetPropertyMappings()
        {
            return _sessionFactory.GetPropertyMappings<T>();
        }

        public string GetIdentifierName()
        {
            return _sessionFactory.GetIdentifierName<T>();
        }

        public string GetTableName()
        {
            return _sessionFactory.GetTableName<T>();
        }
    }
}