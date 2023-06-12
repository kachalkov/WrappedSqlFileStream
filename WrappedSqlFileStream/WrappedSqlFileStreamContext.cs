using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using WrappedSqlFileStream.Mapping;

namespace WrappedSqlFileStream
{

    public class WrappedSqlFileStreamContext<T> : IWrappedSqlFileStreamContext
    {
        public SqlConnection Connection { get; set; }
        public SqlTransaction Transaction { get; set; }
        public string TableName { get; set; }
        public string IdentifierName { get; set; }
        public string FileStreamProperty { get; }
        public string FileStreamName { get; }

        public Dictionary<string, string> Mappings { get; set; }

        private readonly bool _isInternalConnection;

        public WrappedSqlFileStreamContext(IMappingProvider mappingProvider, SqlConnection connection, SqlTransaction transaction)
        {
            Mappings = mappingProvider.GetPropertyMappings();
            TableName = mappingProvider.GetTableName();
            IdentifierName = mappingProvider.GetIdentifierName();
            FileStreamProperty = mappingProvider.GetFileStreamProperty();
            FileStreamName = mappingProvider.GetFileStreamName();
            Connection = connection;
            Transaction = transaction;
        }

        public WrappedSqlFileStreamContext(IMappingProvider mappingProvider, string connectionString)
        {
            Mappings = mappingProvider.GetPropertyMappings();
            TableName = mappingProvider.GetTableName();
            IdentifierName = mappingProvider.GetIdentifierName();
            FileStreamProperty = mappingProvider.GetFileStreamProperty();
            FileStreamName = mappingProvider.GetFileStreamName();
            Connection = new SqlConnection(connectionString);
            Connection.Open();
            Transaction = Connection.BeginTransaction();
            _isInternalConnection = true;
        }

        public void CommitAndDispose()
        {
            if (_isInternalConnection)
            {
                if (Transaction != null)
                {
                    Transaction.Commit();
                    Transaction.Dispose();
                }

                if (Connection != null)
                {
                    Connection.Close();
                    Connection.Dispose();
                }
            }
        }

        public void RollbackAndDispose()
        {
            if (_isInternalConnection)
            {
                if (Transaction != null)
                {
                    Transaction.Rollback();
                    Transaction.Dispose();
                }

                if (Connection != null)
                {
                    Connection.Close();
                    Connection.Dispose();
                }
            }
        }

    }
}