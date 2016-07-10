using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Linq.Expressions;
using WrappedSqlFileStream.Mapping;

namespace WrappedSqlFileStream
{
    public class WrappedSqlFileStreamContext<T> : IWrappedSqlFileStreamContext
    {
        public SqlConnection Connection { get; }
        public SqlTransaction Transaction { get; }
        public string TableName { get; }
        public string IdentifierName { get; }
        public string FileStreamProperty { get; }
        public string FileStreamName { get; }
        public Dictionary<string, string> Mappings { get; }

        private readonly bool _isInternalConnection;

        /// <summary>
        /// Creates a WrappedSqlFileStreamContext that will use an existing SqlConnection and SqlTransaction
        /// </summary>
        /// <param name="mappingProvider"></param>
        /// <param name="connection"></param>
        /// <param name="transaction"></param>
        public WrappedSqlFileStreamContext(IMappingProvider mappingProvider, SqlConnection connection, SqlTransaction transaction)
        {
            Mappings = mappingProvider.GetPropertyMappings();
            TableName = mappingProvider.GetTableName();
            IdentifierName = mappingProvider.GetIdentifierName();
            FileStreamName = mappingProvider.GetFileStreamName();
            FileStreamProperty = mappingProvider.GetFileStreamProperty();
            Connection = connection;
            Transaction = transaction;
        }

        /// <summary>
        /// Creates a WrappedSqlFileStreamContext that will use an internal SqlConnection and SqlTransaction
        /// </summary>
        /// <param name="mappingProvider"></param>
        /// <param name="connectionString"></param>
        public WrappedSqlFileStreamContext(IMappingProvider mappingProvider, string connectionString)
        {
            Mappings = mappingProvider.GetPropertyMappings();
            TableName = mappingProvider.GetTableName();
            IdentifierName = mappingProvider.GetIdentifierName();
            FileStreamName = mappingProvider.GetFileStreamName();
            Connection = new SqlConnection(connectionString);
            Connection.Open();
            Transaction = Connection.BeginTransaction();
            _isInternalConnection = true;
        }

        private struct ParameterMapping
        {
            public int Index { get; }
            public string FieldName { get; }
            public string ParameterName { get; }
            public object Value { get; }

            public ParameterMapping(int index, string fieldName, string parameterName, object value)
            {
                Index = index;
                FieldName = fieldName;
                ParameterName = parameterName;
                Value = value;
            }
        }

        /// <summary>
        /// Creates a new row on the table, setting the FILESTREAM column to an empty binary blob
        /// </summary>
        /// <param name="fileStreamFieldExpression">A property expression identifying the FILESTREAM column</param>
        /// <param name="newObjectExpression">An object initializer expression defining the value that will be used to initialize the columns on the row</param>
        public void CreateRow(Expression<Func<T>> newObjectExpression)
        {
            var paramList = new List<ParameterMapping>();
            int index = 0;

            foreach (var binding in ((MemberInitExpression)newObjectExpression.Body).Bindings)
            {
                var fieldName = Mappings[binding.Member.Name];

                if (binding.BindingType == MemberBindingType.Assignment)
                {
                    var assignment = (MemberAssignment)binding;
                    if (assignment.Expression.Type.IsArray && assignment.Expression.Type.GetElementType() == typeof(byte))
                    {
                    }
                    else
                    {
                        var value = Expression.Lambda(assignment.Expression).Compile().DynamicInvoke();
                        if (assignment.Expression.Type.IsEnum)
                        {
                            value = (int)value;
                        }
                        paramList.Add(new ParameterMapping(index, fieldName.StartsWith("[") ? fieldName : $"[{fieldName}]", fieldName.StartsWith("[") ? $"@{fieldName.Substring(1, fieldName.Length - 2)}" : $"@{fieldName}", value));
                    }
                }
                index++;
            }
            paramList.Add(new ParameterMapping(index, FileStreamName.StartsWith("[") ? FileStreamName : $"[{FileStreamName}]", "0x", null));

            var fields = string.Join(",", paramList.OrderBy(x => x.Index).Select(x => x.FieldName));
            var parameters = string.Join(",", paramList.OrderBy(x => x.Index).Select(x => x.ParameterName));

            string insertFileStreamCommand = $"INSERT INTO {TableName} ({fields}) VALUES ({parameters})";

            using (var command = Connection.CreateCommand())
            {
                try
                {
                    command.Transaction = Transaction;
                    command.CommandType = CommandType.Text;
                    command.CommandText = insertFileStreamCommand;
                    foreach (var parameter in paramList.Where(x => x.ParameterName != "0x"))
                    {
                        command.Parameters.Add(new SqlParameter(parameter.ParameterName, parameter.Value));
                    }
                    command.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }

        public void CreateRow(T newObject)
        {
            var paramList = new List<ParameterMapping>();
            int index = 0;

            foreach (var propertyInfo in typeof(T).GetProperties())
            {
                // Skip the ID field, we're not interested in mapping it when creating a new row
                if (propertyInfo.Name != IdentifierName)
                {
                    var fieldName = Mappings[propertyInfo.Name];
                    if (propertyInfo.PropertyType.IsArray && propertyInfo.PropertyType.GetElementType() == typeof(byte))
                    {
                        paramList.Add(new ParameterMapping(index, $"[{fieldName}]", "0x", null));
                    }
                    else
                    {
                        var value = propertyInfo.GetValue(newObject);
                        if (propertyInfo.PropertyType.IsEnum)
                        {
                            value = (int)value;
                        }
                        paramList.Add(new ParameterMapping(index, $"[{fieldName}]", $"@{fieldName}", value));
                    }
                    index++;
                }
            }

            var fields = string.Join(",", paramList.OrderBy(x => x.Index).Select(x => x.FieldName));
            var parameters = string.Join(",", paramList.OrderBy(x => x.Index).Select(x => x.ParameterName));

            string insertTemporaryFile = $"INSERT INTO {TableName} ({fields}) VALUES ({parameters})";

            using (var command = Connection.CreateCommand())
            {
                try
                {
                    command.Transaction = Transaction;
                    command.CommandType = CommandType.Text;
                    command.CommandText = insertTemporaryFile;
                    foreach (var parameter in paramList.Where(x => x.ParameterName != "0x"))
                    {
                        command.Parameters.Add(new SqlParameter(parameter.ParameterName, parameter.Value));
                    }
                    command.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }

        /// <summary>
        /// Commits the SqlTransaction and closes the SqlConnection if they were internally generated.
        /// </summary>
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

        /// <summary>
        /// Rolls back SqlTransaction and closes the SqlConnection if they were internally generated.
        /// </summary>
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