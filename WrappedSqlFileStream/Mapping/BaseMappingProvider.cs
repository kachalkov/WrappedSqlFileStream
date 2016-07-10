using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace WrappedSqlFileStream.Mapping
{
    public abstract class BaseMappingProvider<T> : IMappingProvider
    {
        protected string _identifier;
        protected string _fileStream;
        protected Dictionary<string, string> _propertyMappings;

        protected BaseMappingProvider(Expression<Func<T, byte[]>> fileStreamFieldExpression) 
        {
            _fileStream = ((MemberExpression)fileStreamFieldExpression.Body).Member.Name;
        }

        public abstract Dictionary<string, string> GetPropertyMappings();

        public abstract string GetIdentifierName();

        public virtual string GetFileStreamName()
        {
            return GetPropertyMappings()[_fileStream];
        }

        public virtual string GetFileStreamProperty()
        {
            return _fileStream;
        }


        public abstract string GetTableName();
    }
}