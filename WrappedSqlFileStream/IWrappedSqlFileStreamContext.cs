using System.Collections.Generic;
using System.Data.SqlClient;

namespace WrappedSqlFileStream
{
    public interface IWrappedSqlFileStreamContext
    {
        SqlConnection Connection { get; set; }

        SqlTransaction Transaction { get; set; }

        string TableName { get; set; }

        string IdentifierName { get; set; }

        Dictionary<string,string> Mappings { get; set; }

        void CommitAndDispose();

        void RollbackAndDispose();
    }
}