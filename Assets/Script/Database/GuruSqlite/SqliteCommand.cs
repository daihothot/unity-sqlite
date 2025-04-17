#nullable enable
using System.Collections.Generic;

namespace GuruSqlite
{
    /// <summary>
    /// Sql command type.
    /// </summary>
    public enum SqlCommandType
    {
        /// <summary>
        /// Such as CREATE TABLE, DROP_INDEX, PRAGMA, etc.
        /// </summary>
        Execute,

        /// <summary>
        /// Insert statement.
        /// </summary>
        Insert,

        /// <summary>
        /// Update statement.
        /// </summary>
        Update,

        /// <summary>
        /// Delete statement.
        /// </summary>
        Delete,

        /// <summary>
        /// Query statement (SELECT).
        /// </summary>
        Query
    }

    /// <summary>
    /// Sql command. Internal only.
    /// </summary>
    public class SqlCommand
    {
        /// <summary>
        /// The command type.
        /// </summary>
        public SqlCommandType Type { get; private set; }

        /// <summary>
        /// The SQL statement.
        /// </summary>
        public string Sql { get; private set; }

        /// <summary>
        /// The SQL arguments.
        /// </summary>
        public object[]? Arguments { get; private set; }

        /// <summary>
        /// Sql command constructor.
        /// </summary>
        public SqlCommand(SqlCommandType type, string sql, object[]? arguments)
        {
            Type = type;
            Sql = sql;
            Arguments = arguments;
        }
    }
}