using System;
using System.Data;

namespace WrappedSqlFileStream.Mapping
{
    public static class DataRecordExtensions
    {
        public static Boolean HasColumn(this IDataRecord record, String colName)
        {
            var index = -1;

            if (record != null)
            {
                try
                {
                    index = record.GetOrdinal(colName);
                }
                catch (IndexOutOfRangeException)
                {
                    // NO OP
                }
            }

            return index != -1;
        }

    }
}