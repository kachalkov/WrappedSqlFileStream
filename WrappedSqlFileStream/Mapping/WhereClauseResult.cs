using System.Collections.Generic;
using System.Data.SqlClient;

namespace WrappedSqlFileStream.Mapping
{
    public class WhereClauseResult
    {
        public string WhereClause { get; set; }
        public List<KeyValuePair<SqlParameter, object>> Parameters { get; set; }
    }
}