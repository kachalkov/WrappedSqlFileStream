using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace WrappedSqlFileStream.Mapping
{
    public class WhereClauseResult
    {
        public string WhereClause { get; set; }
        public List<KeyValuePair<SqlParameter, object>> Parameters { get; set; }
    }
}