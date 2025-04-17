#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace GuruSqlite
{
    public class SqliteUtils
    {
        public static int? FirstIntValue(QueryResult result)
        {
            if (result.Count <= 0) return null;
            var firstRow = result[0];
            return firstRow.Count > 0 ? ParseInt(firstRow.Values.First()) : null;
        }

        private static int? ParseInt(object? value)
        {
            if (value == null) return null;
            if (int.TryParse(value.ToString(), out var result))
            {
                return result;
            }
            return null;
        }
        
        public static bool? GetSqlInTransactionArgument(string sql)
        {
            if (sql.StartsWith("BEGIN", StringComparison.OrdinalIgnoreCase))
                return true;
            if (sql.StartsWith("COMMIT", StringComparison.OrdinalIgnoreCase) ||
                sql.StartsWith("ROLLBACK", StringComparison.OrdinalIgnoreCase))
                return false;
            return null;
        }
    }
}