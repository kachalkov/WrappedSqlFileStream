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
        public SqlConnection Connection { get; set; }
        public SqlTransaction Transaction { get; set; }
        public string TableName { get; set; }
        public string IdentifierName { get; set; }
        public Dictionary<string, string> Mappings { get; set; }

        private bool _isInternalConnection;

        public WrappedSqlFileStreamContext(IMappingProvider mappingProvider, SqlConnection connection, SqlTransaction transaction)
        {
            Mappings = mappingProvider.GetPropertyMappings();
            TableName = mappingProvider.GetTableName();
            IdentifierName = mappingProvider.GetIdentifierName();
            Connection = connection;
            Transaction = transaction;
        }

        public WrappedSqlFileStreamContext(IMappingProvider mappingProvider, string connectionString)
        {
            Mappings = mappingProvider.GetPropertyMappings();
            TableName = mappingProvider.GetTableName();
            Connection = new SqlConnection(connectionString);
            Connection.Open();
            Transaction = Connection.BeginTransaction();
            _isInternalConnection = true;
        }

        private struct ParameterMapping
        {
            public int Index { get; set; }
            public string FieldName { get; set; }
            public string ParameterName { get; set; }
            public object Value { get; set; }

            public ParameterMapping(int index, string fieldName, string parameterName, object value)
            {
                Index = index;
                FieldName = fieldName;
                ParameterName = parameterName;
                Value = value;
            }
        }

        public void CreateRow(Expression<Func<T, byte[]>> fileStreamFieldExpression, Expression<Func<T>> newObjectExpression)
        {
            var paramList = new List<ParameterMapping>();
            int index = 0;

            var fileStreamFieldName = Mappings[((MemberExpression)fileStreamFieldExpression.Body).Member.Name];


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
            paramList.Add(new ParameterMapping(index, fileStreamFieldName.StartsWith("[") ? fileStreamFieldName : $"[{fileStreamFieldName}]", "0x", null));

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

        public void CommitAndDispose()
        {
            if (_isInternalConnection)
            {
                if (Transaction != null)
                {
                    Transaction.Commit();
                    Transaction.Dispose();
                    Transaction = null;
                }

                if (Connection != null)
                {
                    Connection.Close();
                    Connection.Dispose();
                    Connection = null;
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