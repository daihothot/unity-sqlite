#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GuruSqlite
{
    /// <summary>
    /// Insert/Update conflict resolver
    /// </summary>
    public enum ConflictAlgorithm
    {
        /// <summary>
        /// When a constraint violation occurs, an immediate ROLLBACK occurs,
        /// thus ending the current transaction, and the command aborts with a
        /// return code of SQLITE_CONSTRAINT. If no transaction is active
        /// (other than the implied transaction that is created on every command)
        /// then this algorithm works the same as ABORT.
        /// </summary>
        Rollback,

        /// <summary>
        /// When a constraint violation occurs, no ROLLBACK is executed
        /// so changes from prior commands within the same transaction
        /// are preserved. This is the default behavior.
        /// </summary>
        Abort,

        /// <summary>
        /// When a constraint violation occurs, the command aborts with a return
        /// code SQLITE_CONSTRAINT. But any changes to the database that
        /// the command made prior to encountering the constraint violation
        /// are preserved and are not backed out.
        /// </summary>
        Fail,

        /// <summary>
        /// When a constraint violation occurs, the one row that contains
        /// the constraint violation is not inserted or changed.
        /// But the command continues executing normally. Other rows before and
        /// after the row that contained the constraint violation continue to be
        /// inserted or updated normally. No error is returned.
        /// </summary>
        Ignore,

        /// <summary>
        /// When a UNIQUE constraint violation occurs, the pre-existing rows that
        /// are causing the constraint violation are removed prior to inserting
        /// or updating the current row. Thus the insert or update always occurs.
        /// The command continues executing normally. No error is returned.
        /// </summary>
        Replace,
    }

    /// <summary>
    /// SQL command builder for Unity
    /// </summary>
    public class SqlBuilder
    {
        private static readonly string[] ConflictValues = new string[]
        {
            "OR ROLLBACK",
            "OR ABORT",
            "OR FAIL",
            "OR IGNORE",
            "OR REPLACE",
        };

        /// <summary>
        /// The resulting SQL command.
        /// </summary>
        public string Sql { get; private set; } = "";

        /// <summary>
        /// The arguments list.
        /// </summary>
        public object[]? Arguments { get; private set; } = Array.Empty<object>();

        /// <summary>
        /// Used during build if there was a name with an escaped keyword.
        /// </summary>
        public bool HasEscape { get; private set; } = false;

        private SqlBuilder()
        {
        }

        /// <summary>
        /// Convenience method for deleting rows in the database.
        /// </summary>
        /// <param name="table">The table to delete from</param>
        /// <param name="where">The optional WHERE clause to apply when deleting. Passing null will delete all rows.</param>
        /// <param name="whereArgs">You may include ?s in the where clause, which will be replaced by the values from whereArgs.</param>
        /// <returns>A SqlBuilder instance with the generated SQL</returns>
        public static SqlBuilder Delete(string table, string? where = null, object[]? whereArgs = null)
        {
            CheckWhereArgs(whereArgs);
            
            var builder = new SqlBuilder();
            var delete = new StringBuilder();
            delete.Append("DELETE FROM ");
            delete.Append(EscapeName(table));
            WriteClause(delete, " WHERE ", where);
            
            builder.Sql = delete.ToString();
            builder.Arguments = whereArgs != null ? whereArgs.ToArray() : null;
            
            return builder;
        }

        /// <summary>
        /// Build an SQL query string from the given clauses.
        /// </summary>
        /// <param name="table">The table names to compile the query against.</param>
        /// <param name="distinct">True if you want each row to be unique, false otherwise.</param>
        /// <param name="columns">A list of which columns to return. Passing null will return all columns.</param>
        /// <param name="where">A filter declaring which rows to return, formatted as an SQL WHERE clause (excluding the WHERE itself).</param>
        /// <param name="whereArgs">Arguments for the where clause.</param>
        /// <param name="groupBy">A filter declaring how to group rows, formatted as an SQL GROUP BY clause (excluding the GROUP BY itself).</param>
        /// <param name="having">A filter declare which row groups to include, formatted as an SQL HAVING clause (excluding the HAVING itself).</param>
        /// <param name="orderBy">How to order the rows, formatted as an SQL ORDER BY clause (excluding the ORDER BY itself).</param>
        /// <param name="limit">Limits the number of rows returned by the query, formatted as LIMIT clause.</param>
        /// <param name="offset">Specifies the starting position for returning rows, formatted as OFFSET clause.</param>
        /// <returns>A SqlBuilder instance with the generated SQL</returns>
        public static SqlBuilder Query(
            string table,
            bool distinct = false,
            string[]? columns = null,
            string? where = null,
            object[]? whereArgs = null,
            string? groupBy = null,
            string? having = null,
            string? orderBy = null,
            int? limit = null,
            int? offset = null)
        {
            if (groupBy == null && having != null)
            {
                throw new ArgumentException("HAVING clauses are only permitted when using a groupBy clause");
            }

            CheckWhereArgs(whereArgs);

            var builder = new SqlBuilder();
            var query = new StringBuilder();

            query.Append("SELECT ");
            if (distinct)
            {
                query.Append("DISTINCT ");
            }
            
            if (columns != null && columns.Length > 0)
            {
                WriteColumns(query, columns);
            }
            else
            {
                query.Append("* ");
            }
            
            query.Append("FROM ");
            query.Append(EscapeName(table));
            WriteClause(query, " WHERE ", where);
            WriteClause(query, " GROUP BY ", groupBy);
            WriteClause(query, " HAVING ", having);
            WriteClause(query, " ORDER BY ", orderBy);
            
            // See https://sqlite.org/lang_select.html
            // offset cannot be specified without limit so ensure to set limit to -1 if not set
            if (limit != null || offset != null)
            {
                WriteClause(query, " LIMIT ", (limit ?? -1).ToString());
            }
            
            if (offset != null)
            {
                WriteClause(query, " OFFSET ", offset.ToString());
            }

            builder.Sql = query.ToString();
            builder.Arguments = whereArgs?.ToArray();
            
            return builder;
        }

        /// <summary>
        /// Convenience method for inserting a row into the database.
        /// </summary>
        /// <param name="table">The table to insert the row into</param>
        /// <param name="values">This dictionary contains the initial column values for the row. The keys should be the column names and the values the column values</param>
        /// <param name="nullColumnHack">Optional; may be null. SQL doesn't allow inserting a completely empty row without naming at least one column name.</param>
        /// <param name="conflictAlgorithm">Algorithm to use when a constraint conflict occurs</param>
        /// <returns>A SqlBuilder instance with the generated SQL</returns>
        public static SqlBuilder Insert(
            string table,
            Dictionary<string, object?> values,
            string? nullColumnHack = null,
            ConflictAlgorithm? conflictAlgorithm = null)
        {
            var builder = new SqlBuilder();
            var insert = new StringBuilder();
            
            insert.Append("INSERT");
            if (conflictAlgorithm != null)
            {
                insert.Append($" {ConflictValues[(int)conflictAlgorithm]}");
            }
            
            insert.Append(" INTO ");
            insert.Append(EscapeName(table));
            insert.Append(" (");

            List<object>? bindArgs = null;
            var size = values.Count;

            if (size > 0)
            {
                var sbValues = new StringBuilder(") VALUES (");
                bindArgs = new List<object>();
                var i = 0;
                
                foreach (var (colName, value) in values)
                {
                    if (i++ > 0)
                    {
                        insert.Append(", ");
                        sbValues.Append(", ");
                    }

                    insert.Append(EscapeName(colName));
                    
                    if (value == null)
                    {
                        sbValues.Append("NULL");
                    }
                    else
                    {
                        CheckNonNullValue(value);
                        bindArgs.Add(value);
                        sbValues.Append("?");
                    }
                }
                
                insert.Append(sbValues);
            }
            else
            {
                if (nullColumnHack == null)
                {
                    throw new ArgumentException("nullColumnHack required when inserting no data");
                }
                
                insert.Append($"{nullColumnHack}) VALUES (NULL");
            }
            
            insert.Append(")");

            builder.Sql = insert.ToString();
            builder.Arguments = bindArgs?.ToArray();
            
            return builder;
        }

        /// <summary>
        /// Convenience method for updating rows in the database.
        /// </summary>
        /// <param name="table">The table to update in</param>
        /// <param name="values">A dictionary from column names to new column values. null is a valid value that will be translated to NULL.</param>
        /// <param name="where">The optional WHERE clause to apply when updating. Passing null will update all rows.</param>
        /// <param name="whereArgs">You may include ?s in the where clause, which will be replaced by the values from whereArgs.</param>
        /// <param name="conflictAlgorithm">Algorithm to use when a constraint conflict occurs</param>
        /// <returns>A SqlBuilder instance with the generated SQL</returns>
        public static SqlBuilder Update(
            string table,
            Dictionary<string, object?> values,
            string? where = null,
            object[]? whereArgs = null,
            ConflictAlgorithm? conflictAlgorithm = null)
        {
            if (values.Count == 0)
            {
                throw new ArgumentException("Empty values");
            }
            
            CheckWhereArgs(whereArgs);

            var builder = new SqlBuilder();
            var update = new StringBuilder();
            
            update.Append("UPDATE");
            if (conflictAlgorithm != null)
            {
                update.Append($" {ConflictValues[(int)conflictAlgorithm]}");
            }
            
            update.Append($" {EscapeName(table)}");
            update.Append(" SET ");

            var bindArgs = new List<object>();
            var i = 0;

            foreach (var colName in values.Keys)
            {
                update.Append(i++ > 0 ? ", " : "");
                update.Append(EscapeName(colName));
                
                var value = values[colName];
                
                if (value != null)
                {
                    CheckNonNullValue(value);
                    bindArgs.Add(value);
                    update.Append(" = ?");
                }
                else
                {
                    update.Append(" = NULL");
                }
            }

            if (whereArgs != null)
            {
                bindArgs.AddRange(whereArgs);
            }

            WriteClause(update, " WHERE ", where);

            builder.Sql = update.ToString();
            builder.Arguments = bindArgs.ToArray();
            
            return builder;
        }

        #region Helper Methods

        private static void WriteClause(StringBuilder s, string name, string? clause)
        {
            if (clause == null) return;
            s.Append(name);
            s.Append(clause);
        }

        private static void WriteColumns(StringBuilder s, string[] columns)
        {
            var n = columns.Length;

            for (var i = 0; i < n; i++)
            {
                var column = columns[i];

                if (i > 0)
                {
                    s.Append(", ");
                }
                
                s.Append(EscapeName(column));
            }
            
            s.Append(" ");
        }

        private static void CheckWhereArgs(object[]? whereArgs)
        {
            if (whereArgs == null) return;
            foreach (var arg in whereArgs)
            {
                CheckNonNullValue(arg);
            }
        }

        private static void CheckNonNullValue(object? value)
        {
            if (value is not string && 
                value is not int && 
                value is not long && 
                value is not double && 
                value is not float && 
                value is not bool && 
                value is not byte[])
            {
                throw new ArgumentException($"Unsupported value type: {value?.GetType().Name}");
            }
        }

        #endregion

        #region Escaping Methods

        /// <summary>
        /// True if a name had been escaped already.
        /// </summary>
        /// <param name="name">The name to check</param>
        /// <returns>True if already escaped</returns>
        public static bool IsEscapedName(string name)
        {
            if (name.Length < 2) return false;
            if (AreCodeUnitsEscaped(name))
            {
                return EscapeNames.Contains(name.Substring(1, name.Length - 2).ToLowerInvariant());
            }
            return false;
        }

        // The actual escape implementation
        // We use double quote, although backtick could be used too
        private static string DoEscape(string name) => $"\"{name}\"";

        /// <summary>
        /// Escape a table or column name if necessary.
        /// i.e. if it is an identified it will be surrounded by " (double-quote)
        /// Only some name belonging to keywords can be escaped
        /// </summary>
        /// <param name="name">The name to escape</param>
        /// <returns>Escaped name if needed</returns>
        private static string EscapeName(string name)
        {
            return EscapeNames.Contains(name.ToLowerInvariant()) ? DoEscape(name) : name;
        }

        /// <summary>
        /// Unescape a table or column name.
        /// </summary>
        /// <param name="name">The name to unescape</param>
        /// <returns>Unescaped name</returns>
        public static string UnescapeName(string name)
        {
            return IsEscapedName(name) ? name.Substring(1, name.Length - 2) : name;
        }

        /// <summary>
        /// Escape a column name if necessary.
        /// Only for insert and update keys
        /// </summary>
        /// <param name="name">The entity name to escape</param>
        /// <returns>Escaped entity name</returns>
        public static string EscapeEntityName(string name)
        {
            return EntityNameNeedEscape(name) ? DoEscape(name) : name;
        }

        private static bool AreCodeUnitsEscaped(string str)
        {
            if (string.IsNullOrEmpty(str)) return false;
            var first = str[0];
                
            switch (first)
            {
                case '"':
                case '`':
                    var last = str[str.Length - 1];
                    return last == first;
                case '\'':
                    // not yet
                    break;
            }
            return false;
        }

        private static bool EntityNameNeedEscape(string name)
        {
            // We need to escape if not escaped yet and if not a valid keyword
            if (EscapeNames.Contains(name.ToLowerInvariant()))
            {
                return true;
            }

            // Must start with a alpha or underscore
            if (!IsAlphaOrUnderscore(name[0]))
            {
                return true;
            }
            
            for (int i = 1; i < name.Length; i++)
            {
                char c = name[i];
                if (!IsAlphaOrUnderscore(c) && !IsDigit(c))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsDigit(char c) => c >= '0' && c <= '9';

        private static bool IsAlphaOrUnderscore(char c) =>
            c is >= 'a' and <= 'z' ||
            c is >= 'A' and <= 'Z' ||
            c == '_';

        /// <summary>
        /// Unescape a value key name.
        /// </summary>
        /// <param name="name">The name to unescape</param>
        /// <returns>Unescaped value key name</returns>
        public static string UnescapeValueKeyName(string name)
        {
            return AreCodeUnitsEscaped(name) ? name.Substring(1, name.Length - 2) : name;
        }
        
        /// <summary>
        /// SQLite keywords to escape.
        /// </summary>
        private static readonly HashSet<string> EscapeNames = new()
        {
            "abort", "action", "add", "after", "all", "alter", "always", "analyze", "and", "as", "asc",
            "attach", "autoincrement", "before", "begin", "between", "by", "cascade", "case", "cast", "check",
            "collate", "column", "commit", "conflict", "constraint", "create", "cross", "current", "current_date",
            "current_time", "current_timestamp", "database", "default", "deferrable", "deferred", "delete", "desc",
            "detach", "distinct", "do", "drop", "each", "else", "end", "escape", "except", "exclude", "exclusive",
            "exists", "explain", "fail", "filter", "first", "following", "for", "foreign", "from", "full", "generated",
            "glob", "group", "groups", "having", "if", "ignore", "immediate", "in", "index", "indexed", "initially",
            "inner", "insert", "instead", "intersect", "into", "is", "isnull", "join", "key", "last", "left", "like",
            "limit", "match", "materialized", "natural", "no", "not", "nothing", "notnull", "null", "nulls", "of",
            "offset", "on", "or", "order", "others", "outer", "over", "partition", "plan", "pragma", "preceding",
            "primary", "query", "raise", "range", "recursive", "references", "regexp", "reindex", "release", "rename",
            "replace", "restrict", "returning", "right", "rollback", "row", "rows", "savepoint", "select", "set",
            "table", "temp", "temporary", "then", "ties", "to", "transaction", "trigger", "unbounded", "union",
            "unique", "update", "using", "vacuum", "values", "view", "virtual", "when", "where", "window", "with",
            "without"
        };

        #endregion
    }
}