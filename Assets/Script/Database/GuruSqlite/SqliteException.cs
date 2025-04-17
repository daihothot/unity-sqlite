#nullable enable
namespace GuruSqlite
{
    using System;
    using System.Collections.Generic;

    public abstract class DatabaseException : Exception
    {
        protected readonly string? _message;

        protected DatabaseException(string? message)
        {
            _message = message;
        }

        public override string ToString()
        {
            return $"DatabaseException({_message})";
        }

        public bool IsNoSuchTableError(string? table = null)
        {
            if (_message == null) return false;
            var expected = "no such table: ";
            if (table != null)
            {
                expected += table;
            }

            return _message.Contains(expected);
        }

        public bool IsDuplicateColumnError(string? column = null)
        {
            if (_message == null) return false;
            var expected = "duplicate column name: ";
            if (column != null)
            {
                expected += column;
            }

            return _message.Contains(expected);
        }

        public bool IsSyntaxError()
        {
            return _message != null && _message.Contains("syntax error");
        }

        public bool IsOpenFailedError()
        {
            return _message != null && _message.Contains("open_failed");
        }

        public bool IsDatabaseClosedError()
        {
            if (_message != null)
            {
                return _message.Contains("database_closed") ||
                       _message.Contains("This database has already been closed");
            }

            return false;
        }

        public bool isReadOnlyError()
        {
            return _message != null && _message.Contains("readonly");
        }

        public bool IsUniqueConstraintError(string? field = null)
        {
            if (_message == null) return false;
            var expected = "UNIQUE constraint failed: ";
            if (field != null)
            {
                expected += field;
            }

            return _message.ToLower().Contains(expected.ToLower());
        }

        public bool IsNotNullConstraintError(string? field = null)
        {
            if (_message == null) return false;
            var expected = "NOT NULL constraint failed: ";
            if (field != null)
            {
                expected += field;
            }

            return _message.ToLower().Contains(expected.ToLower());
        }

        public abstract int? GetResultCode();

        public abstract object? Result { get; }
    }

    public class SqliteDatabaseException : DatabaseException
    {
        private int? _resultCode;
        private bool? _transactionClosed;

        public override object? Result { get; }

        public SqliteDatabaseException(
            string? message,
            object? result,
            int? resultCode = null,
            bool? transactionClosed = null
        ) : base(message)
        {
            this.Result = result;
            _resultCode = resultCode;
            _transactionClosed = transactionClosed;
        }

        // A placeholder method for converting argument lists to string if needed.
        // Adjust or implement as desired.
        private static string ArgumentsToString(IEnumerable<object?> args)
        {
            return $"[{string.Join(", ", args)}]";
        }

        // Suppose these keys come from elsewhere; define them here for convenience.
        private const string ParamSql = "sql";
        private const string ParamSqlArguments = "args";

        private Dictionary<string, object?>? ResultMap => Result as Dictionary<string, object?>;

        public override string ToString()
        {
            if (ResultMap == null) return base.ToString();
            if (!ResultMap.ContainsKey(ParamSql) || ResultMap[ParamSql] == null) return base.ToString();
            var args = ResultMap.ContainsKey(ParamSqlArguments)
                ? ResultMap[ParamSqlArguments]
                : null;
            if (args is List<object?> listArgs)
            {
                return $"DatabaseException({_message}) sql '{ResultMap[ParamSql]}' args {ArgumentsToString(listArgs)}";
            }
            else
            {
                return $"DatabaseException({_message}) sql '{ResultMap[ParamSql]}'";
            }
        }

        public override int? GetResultCode()
        {
            if (_resultCode != null)
            {
                return _resultCode;
            }

            var lowerMessage = _message?.ToLower();
            if (lowerMessage == null) return null;

            var code = FindCode("(sqlite code ");
            if (code != null)
            {
                _resultCode = code;
                return code;
            }

            code = FindCode("(code ");
            if (code != null)
            {
                _resultCode = code;
                return code;
            }

            // iOS
            code = FindCode("code=");
            if (code == null) return null;
            _resultCode = code;
            return code;

            int? FindCode(string patternPrefix)
            {
                var index = lowerMessage.IndexOf(patternPrefix, StringComparison.Ordinal);
                if (index == -1) return null;
                try
                {
                    var codePart = lowerMessage.Substring(index + patternPrefix.Length).Trim().Split(' ')[0];
                    var endIndex = codePart.IndexOf(')');
                    if (endIndex != -1)
                    {
                        codePart = codePart.Substring(0, endIndex);
                    }

                    var parsedCode = int.TryParse(codePart, out var rc) ? rc : (int?)null;
                    if (parsedCode != null)
                    {
                        return parsedCode;
                    }
                }
                catch
                {
                    // Ignore parsing errors
                }

                return null;
            }
        }

        public bool TransactionClosed
        {
            get => _transactionClosed ?? false;
            set => _transactionClosed = value;
        }
    }

    public class SqliteTransactionRollbackSuccess<T> : Exception
    {
        public T Result { get; private set; }

        public SqliteTransactionRollbackSuccess(T result)
        {
            Result = result;
        }
    }
}