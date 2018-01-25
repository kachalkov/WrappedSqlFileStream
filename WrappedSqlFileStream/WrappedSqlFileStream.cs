using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using WrappedSqlFileStream.Mapping;

namespace WrappedSqlFileStream
{
    /// <summary>
    /// Wraps a SqlFileStream along with row-related data which is mapped to type <see cref="T"/>
    /// </summary>
    public class WrappedSqlFileStream<T> : Stream
    {
        #region Private Fields
        private readonly SqlFileStream _sqlFileStream;
        private readonly IWrappedSqlFileStreamContext _context;
        private FileMode _fileMode;
        private readonly FileAccess _fileAccess;
        private bool _truncate;
        private bool _mustNotExist;

        #endregion

        #region Public Properties
        /// <summary>
        /// The action to perform before the transaction is committed and the connection is closed when the Dispose method is called on the stream
        /// </summary>
        public Action<IWrappedSqlFileStreamContext> OnDispose { get; set; }

        // The fields associated with the SqlFileStream
        public T RowData { get; set; }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new WrappedSqlFileStream for Reading or Writing an existing FILESTREAM
        /// </summary>
        /// <param name="context">The context of the WrappedSqlFileStream containing the table mappings and the connection and transaction</param>
        /// <param name="whereExpression">An expression identifying the row. Currently only simple equality and boolean expressions are supported</param>
        /// <param name="fileMode"></param>
        /// <param name="fileAccess">The FileAccess mode to use when creating the stream</param>
        public WrappedSqlFileStream(IWrappedSqlFileStreamContext context,
            Expression<Func<T, bool>> whereExpression,
            FileMode fileMode,
            FileAccess fileAccess)
        {
            switch (fileMode)
            {
                case FileMode.Create:
                case FileMode.CreateNew:
                case FileMode.OpenOrCreate:
                    throw new ArgumentException("Do not use this constructor when creating a new FILESTREAM", nameof(fileAccess));
                case FileMode.Truncate:
                    TruncateIfExists(whereExpression);
                    break;
                case FileMode.Open:
                    break;
                case FileMode.Append:
                    if (fileAccess == FileAccess.Read)
                    {
                        throw new ArgumentException("FileMode.Append is not allowed with FileAccess.Read");
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(fileMode), fileMode, null);
            }

            _context = context;
            _fileMode = fileMode;
            _fileAccess = fileAccess;
            _sqlFileStream = OpenFileStream(whereExpression);

            if (fileAccess == FileAccess.Write && fileMode == FileMode.Append)
            {
                _sqlFileStream.Seek(0, SeekOrigin.End);
            }
        }

        /// <summary>
        /// Creates a new WrappedSqlFileStream for Write access
        /// </summary>
        /// <param name="context">The context of the WrappedSqlFileStream containing the table mappings and the connection and transaction</param>
        /// <param name="whereExpression">An expression identifying the row. Currently only simple equality and boolean expressions are supported</param>
        /// <param name="fileMode"></param>
        /// <param name="fileAccess">The FileAccess mode to use when creating the stream</param>
        /// <param name="newObjectExpression"></param>
        public WrappedSqlFileStream(IWrappedSqlFileStreamContext context, Expression<Func<T, bool>> whereExpression, FileMode fileMode, FileAccess fileAccess, Expression<Func<T>> newObjectExpression)
        {
            _context = context;
            _fileMode = fileMode;
            _fileAccess = fileAccess;
            switch (fileMode)
            {
                case FileMode.Open:

                    break;
                case FileMode.CreateNew:
                    CreateRow(newObjectExpression);
                    break;
                case FileMode.Create:
                    CreateRow(newObjectExpression, whereExpression, true);
                    break;
                case FileMode.OpenOrCreate:
                    CreateRow(newObjectExpression, whereExpression);
                    break;
                case FileMode.Truncate:
                    break;
                case FileMode.Append:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(fileMode), fileMode, null);
            }
            if (fileMode == FileMode.Create)
            {
                CreateRow(newObjectExpression, whereExpression, true);
            }

            _sqlFileStream = OpenFileStream(whereExpression);

            if (fileAccess == FileAccess.Write && fileMode == FileMode.Append)
            {
                _sqlFileStream.Seek(0, SeekOrigin.End);
            }
        }

        #endregion

        #region Overriden Stream methods

        public override void Flush()
        {
            _sqlFileStream.Flush();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _sqlFileStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _sqlFileStream.SetLength(value);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _sqlFileStream.Read(buffer, offset, count);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _sqlFileStream.Write(buffer, offset, count);
        }

        public override bool CanRead
        {
            get { return _sqlFileStream.CanRead; }
        }

        public override bool CanSeek
        {
            get { return _sqlFileStream.CanSeek; }
        }

        public override bool CanWrite
        {
            get { return _sqlFileStream.CanWrite; }
        }

        public override long Length
        {
            get { return _sqlFileStream.Length; }
        }

        public override long Position
        {
            get { return _sqlFileStream.Position; }
            set { _sqlFileStream.Position = value; }
        }

        #endregion

        #region Overridden Dispose()

        protected override void Dispose(bool disposing)
        {
            if (_sqlFileStream != null)
            {
                _sqlFileStream.Close();
                _sqlFileStream.Dispose();
            }

            OnDispose?.Invoke(_context);

            _context.CommitAndDispose();
        }

        #endregion

        #region Private Methods

        private ICollection<ParameterMapping> GetParametersForNewExpression(Expression<Func<T>> newObjectExpression)
        {
            var paramList = new List<ParameterMapping>();
            int index = 0;

            foreach (var binding in ((MemberInitExpression)newObjectExpression.Body).Bindings)
            {
                var fieldName = _context.Mappings[binding.Member.Name];

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
            paramList.Add(new ParameterMapping(index, _context.FileStreamName.StartsWith("[") ? _context.FileStreamName : $"[{_context.FileStreamName}]", "0x", null));

            return paramList;
        }

        private void CreateRow(Expression<Func<T>> newObjectExpression, Expression<Func<T, bool>> whereExpression = null, bool truncateIfExists = false)
        {
            var streamField = _context.FileStreamName.StartsWith("[") ? _context.FileStreamName : $"[{_context.FileStreamName}]";
            string fields = "";
            string parameters = "";
            ICollection<ParameterMapping> paramList = null;
            if (newObjectExpression != null)
            {
                paramList = GetParametersForNewExpression(newObjectExpression);

                fields = string.Join(",", paramList.OrderBy(x => x.Index).Select(x => x.FieldName));
                parameters = string.Join(",", paramList.OrderBy(x => x.Index).Select(x => x.ParameterName));
            }

            string existsCondition = "";
            WhereClauseResult existsWhereClause = null;
            if (whereExpression != null)
            {
                var whereMapper = new SqlWhereClauseMapper<T>(_context.Mappings);
                existsWhereClause = whereMapper.Map(whereExpression.Body);
                if (newObjectExpression != null)
                {
                    existsCondition = $"IF NOT EXISTS (SELECT 1 FROM {_context.TableName} WHERE {existsWhereClause.WhereClause}) ";
                }
                else
                {
                    existsCondition = $"IF EXISTS (SELECT 1 FROM {_context.TableName} WHERE {existsWhereClause.WhereClause}) ";
                }
            }

            string insertTemporaryFile = existsCondition + $"INSERT INTO {_context.TableName} ({fields}) VALUES ({parameters})";

            if (existsWhereClause != null && truncateIfExists)
            {
                if (newObjectExpression != null)
                {
                    insertTemporaryFile += $" ELSE UPDATE {_context.TableName} SET {streamField} = 0x WHERE {existsWhereClause.WhereClause}";
                }
                else
                {
                    insertTemporaryFile += $" UPDATE {_context.TableName} SET {streamField} = 0x WHERE {existsWhereClause.WhereClause}";
                }
            }

            using (var command = _context.Connection.CreateCommand())
            {
                try
                {
                    command.Transaction = _context.Transaction;
                    command.CommandType = CommandType.Text;
                    command.CommandText = insertTemporaryFile;
                    if (existsWhereClause != null)
                    {
                        foreach (var parameter in existsWhereClause.Parameters)
                        {
                            command.Parameters.Add(parameter.Key).Value = parameter.Value;
                        }
                    }
                    if (paramList != null)
                    {
                        foreach (var parameter in paramList.Where(x => x.ParameterName != "0x"))
                        {
                            command.Parameters.Add(new SqlParameter(parameter.ParameterName, parameter.Value));
                        }
                    }
                    command.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }


        private void TruncateIfExists(Expression<Func<T, bool>> whereExpression)
        {
            var streamField = _context.FileStreamName.StartsWith("[") ? _context.FileStreamName : $"[{_context.FileStreamName}]";
            WhereClauseResult existsWhereClause = null;
            string query = "";

            var whereMapper = new SqlWhereClauseMapper<T>(_context.Mappings);
            existsWhereClause = whereMapper.Map(whereExpression.Body);
            query = $"IF EXISTS (SELECT 1 FROM {_context.TableName} WHERE {existsWhereClause.WhereClause}) " + $"UPDATE {_context.TableName} SET {streamField} = 0x WHERE {existsWhereClause.WhereClause}";

            using (var command = _context.Connection.CreateCommand())
            {
                try
                {
                    command.Transaction = _context.Transaction;
                    command.CommandType = CommandType.Text;
                    command.CommandText = query;
                    foreach (var parameter in existsWhereClause.Parameters)
                    {
                        command.Parameters.Add(parameter.Key).Value = parameter.Value;
                    }
                    command.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }

        private void CreateRow(T newObject)
        {
            var paramList = new List<ParameterMapping>();
            int index = 0;

            foreach (var propertyInfo in typeof(T).GetProperties())
            {
                // Skip the ID field, we're not interested in mapping it when creating a new row
                if (propertyInfo.Name != _context.IdentifierName)
                {
                    var fieldName = _context.Mappings[propertyInfo.Name];
                    if (propertyInfo.PropertyType.IsArray && propertyInfo.PropertyType.GetElementType() == typeof(byte))
                    {
                        paramList.Add(new ParameterMapping(index, $"[{fieldName}]", "0x", null));
                    }
                    else
                    {
#if NET40
                        var value = propertyInfo.GetValue(newObject, null);
#else
                        var value = propertyInfo.GetValue(newObject);
#endif
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

            string insertTemporaryFile = $"INSERT INTO {_context.TableName} ({fields}) VALUES ({parameters})";

            using (var command = _context.Connection.CreateCommand())
            {
                try
                {
                    command.Transaction = _context.Transaction;
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

        private SqlFileStream OpenFileStream(Expression<Func<T, bool>> whereExpression)
        {
            var fields = "";
            var tableName = _context.TableName;
            var streamProperty = _context.FileStreamProperty;
            var streamField = _context.FileStreamName.StartsWith("[") ? _context.FileStreamName : $"[{_context.FileStreamName}]";
            var mappings = _context.Mappings;

            if (_fileAccess == FileAccess.Read)
            {
                fields = ", " + string.Join(", ", mappings.Where(x => x.Key != streamProperty).Select(x => x.Value));
            }

            var whereMapper = new SqlWhereClauseMapper<T>(mappings);
            var whereClause = whereMapper.Map(whereExpression.Body);

            var sqlQuery = $"SELECT {streamField}.PathName(), GET_FILESTREAM_TRANSACTION_CONTEXT() {fields} " + $"FROM {tableName} WHERE {whereClause.WhereClause}";

            SqlDataReader sqlDataReader;

            using (var sqlCommand = new SqlCommand(sqlQuery, _context.Connection, _context.Transaction))
            {
                foreach (var parameter in whereClause.Parameters)
                {
                    sqlCommand.Parameters.Add(parameter.Key).Value = parameter.Value;
                }
                sqlDataReader = sqlCommand.ExecuteReader();
            }

            sqlDataReader.Read();

            var serverPath = sqlDataReader.GetSqlString(0).Value;
            var serverTransactionContext = sqlDataReader.GetSqlBinary(1).Value;

            if (_fileAccess == FileAccess.Read)
            {
                var mapper = new SqlDataMapper<T>(mappings);
                RowData = mapper.Map(sqlDataReader);
            }

            sqlDataReader.Close();

            return new SqlFileStream(serverPath, serverTransactionContext, _fileAccess);
        }

#endregion

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
    }
}