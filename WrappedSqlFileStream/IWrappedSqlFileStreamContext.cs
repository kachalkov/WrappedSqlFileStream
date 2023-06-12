using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace WrappedSqlFileStream
{
    public interface IWrappedSqlFileStreamContext
    {
        SqlConnection Connection { get; }

        SqlTransaction Transaction { get; }

        string TableName { get; }

        string IdentifierName { get; }
        string FileStreamProperty { get; }
        string FileStreamName { get; }

        Dictionary<string,string> Mappings { get; }

        void CommitAndDispose();

        void RollbackAndDispose();
    }
}