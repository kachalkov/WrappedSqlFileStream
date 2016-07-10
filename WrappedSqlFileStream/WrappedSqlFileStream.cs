using System;
using System.Collections.Generic;
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
        private readonly SqlFileStream _sqlFileStream;

        private IWrappedSqlFileStreamContext _context;
            
        /// <summary>
        /// The action to perform before the transaction is committed and the connection is closed when the Dispose method is called on the stream
        /// </summary>
        public Action<IWrappedSqlFileStreamContext> OnDispose { get; set; }

        // The fields associated with the SqlFileStream
        public T RowData { get; set; }

        /// <summary>
        /// Creates a new WrappedSqlFileStream <see cref="T"/> 
        /// </summary>
        /// <param name="context">The context of the WrappedSqlFileStream containing the table mappings and the connection and transaction</param>
        /// <param name="whereExpression">An expression identifying the row. Currently only simple equality and boolean expressions are supported</param>
        /// <param name="fileAccess">The FileAccess mode to use when creating the stream</param>
        public WrappedSqlFileStream(IWrappedSqlFileStreamContext context,
            Expression<Func<T, bool>> whereExpression,
            FileAccess fileAccess)
        {
            _context = context;
            _sqlFileStream = CreateStream(context.TableName, whereExpression, fileAccess, context.Mappings);
        }

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

        private SqlFileStream CreateStream(string tableName,
            Expression<Func<T, bool>> whereExpression,
            FileAccess fileAccess,
            Dictionary<string, string> mappings)
        {
            string fields = "";

            var streamProperty = _context.FileStreamProperty;
            var streamField = _context.FileStreamName;

            if (fileAccess == FileAccess.Read)
            {
                fields = ", " + string.Join(", ", mappings.Where(x => x.Key != streamProperty).Select(x => x.Value));
            }

            var whereMapper = new SqlWhereClauseMapper<T>(mappings);
            var whereClause = whereMapper.Map(whereExpression.Body);

            var sqlQuery = $"SELECT {streamField}.PathName(), GET_FILESTREAM_TRANSACTION_CONTEXT() {fields} " +
                              $"FROM {tableName} WHERE {whereClause.WhereClause}";

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

            if (fileAccess == FileAccess.Read)
            {
                var mapper = new SqlDataMapper<T>(mappings);
                RowData = mapper.Map(sqlDataReader);
            }

            sqlDataReader.Close();

            return new SqlFileStream(serverPath, serverTransactionContext, fileAccess);
        }
    }
}