#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Mono.Data.Sqlite;
using UnityEngine;

namespace GuruSqlite
{
    public class EditorDatabase
    {
        // Constants
        private const string TAG = "EditorDatabase";

        // Static fields matching Java implementation
        private static readonly bool WAL_ENABLED_BY_DEFAULT = false;
        private static readonly string WAL_ENABLED_META_NAME = "guru.core.sqlite.wal_enabled";
        private static bool? walGloballyEnabled;

        // Database specific fields
        internal readonly string path;
        private readonly int id;
        private readonly int logLevel;
        private readonly bool singleInstance;

        // Connection and transaction state
        private SqliteConnection sqliteConnection;
        private int transactionDepth = 0;
        private int lastTransactionId = 0;
        private int? currentTransactionId;

        // Cursor management
        private int lastCursorId = 0;
        private readonly Dictionary<int, EditorSqliteCursor> cursors = new Dictionary<int, EditorSqliteCursor>();

        // Operation queue
        private readonly List<QueuedOperation> noTransactionOperationQueue = new List<QueuedOperation>();

        // Worker pool for async operations
        public IDatabaseWorkerPool databaseWorkerPool;

        public EditorDatabase(string path, int id, bool singleInstance, int logLevel)
        {
            this.path = path;
            this.id = id;
            this.singleInstance = singleInstance;
            this.logLevel = logLevel;
        }

        #region Static Methods

        public static bool CheckWalEnabled()
        {
            return CheckMetaBoolean(WAL_ENABLED_META_NAME, WAL_ENABLED_BY_DEFAULT);
        }

        private static bool CheckMetaBoolean(string metaKey, bool defaultValue)
        {
            try
            {
                // In Unity, we can use PlayerPrefs or EditorPrefs to simulate this
                // For now, just use the default value
                return defaultValue;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return defaultValue;
            }
        }

        public static void DeleteDatabase(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public static bool ExistsDatabase(string path)
        {
            try
            {
                return File.Exists(path);
            }
            catch (Exception)
            {
                return false;
            }
        }

        #endregion

        #region Database Connection Methods

        public void Open()
        {
            try
            {
                // Check meta data only once
                if (walGloballyEnabled == null)
                {
                    walGloballyEnabled = CheckWalEnabled();
                    if (walGloballyEnabled.Value && LogLevel.HasVerboseLevel(logLevel))
                    {
                        Debug.Log($"[{GetThreadLogTag()}] [sqflite] WAL enabled");
                    }
                }

                // Create connection string
                string connectionString = $"Data Source={path};Version=3;";
                if (walGloballyEnabled.Value)
                {
                    connectionString += "Journal Mode=WAL;";
                }

                // Create directory if it doesn't exist
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                sqliteConnection = new SqliteConnection(connectionString);
                sqliteConnection.Open();
            }
            catch (Exception e)
            {
                Debug.LogError($"[{GetThreadLogTag()}] Failed to open database: {e.Message}");
                throw;
            }
        }

        public void OpenReadOnly()
        {
            try
            {
                var connectionString = $"Data Source={path};Version=3;Read Only=True;";
                sqliteConnection = new SqliteConnection(connectionString);
                sqliteConnection.Open();
            }
            catch (Exception e)
            {
                Debug.LogError($"[{GetThreadLogTag()}] Failed to open database in read-only mode: {e.Message}");
                throw;
            }
        }

        public void Close()
        {
            try
            {
                if (cursors.Count > 0 && LogLevel.HasSqlLevel(logLevel))
                {
                    Debug.Log($"[{GetThreadLogTag()}] {cursors.Count} cursor(s) are left opened");
                }

                sqliteConnection?.Close();
                sqliteConnection?.Dispose();
                sqliteConnection = null;
            }
            catch (Exception e)
            {
                Debug.LogError($"[{GetThreadLogTag()}] Failed to close database: {e.Message}");
            }
        }

        public SqliteConnection GetWritableDatabase()
        {
            return sqliteConnection;
        }

        public SqliteConnection GetReadableDatabase()
        {
            return sqliteConnection;
        }

        public bool EnableWriteAheadLogging()
        {
            try
            {
                using (var command = sqliteConnection.CreateCommand())
                {
                    command.CommandText = "PRAGMA journal_mode=WAL;";
                    string result = (string)command.ExecuteScalar();
                    return result?.ToLowerInvariant() == "wal";
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[{GetThreadLogTag()}] Enable WAL error: {e.Message}");
                return false;
            }
        }

        #endregion

        #region Logging

        private string GetThreadLogTag()
        {
            return $"{id},{System.Threading.Thread.CurrentThread.ManagedThreadId}";
        }

        private string GetThreadLogPrefix()
        {
            return $"[{GetThreadLogTag()}] ";
        }

        #endregion

        #region Cursor Operations

        private Dictionary<string, object> CursorToResults(SqliteDataReader reader, int? cursorPageSize)
        {
            var results = new Dictionary<string, object>();
            var rows = new List<List<object>>();
            var columns = new List<string>();

            // Get column names
            for (var i = 0; i < reader.FieldCount; i++)
            {
                columns.Add(reader.GetName(i));
            }

            // Get rows
            var rowCount = 0;
            var cursorHasMoreData = false;

            while (reader.Read())
            {
                var row = new List<object>();
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    if (reader.IsDBNull(i))
                    {
                        row.Add(null);
                    }
                    else
                    {
                        // Convert to appropriate type
                        switch (reader.GetFieldType(i).Name)
                        {
                            case "Int64":
                                row.Add(reader.GetInt64(i));
                                break;
                            case "Int32":
                                row.Add(reader.GetInt32(i));
                                break;
                            case "Double":
                                row.Add(reader.GetDouble(i));
                                break;
                            case "String":
                                row.Add(reader.GetString(i));
                                break;
                            case "Boolean":
                                row.Add(reader.GetBoolean(i));
                                break;
                            case "Byte[]":
                                row.Add(reader.GetValue(i));
                                break;
                            default:
                                row.Add(reader.GetValue(i));
                                break;
                        }
                    }
                }

                rows.Add(row);
                rowCount++;

                // Handle paging
                if (!cursorPageSize.HasValue || rowCount < cursorPageSize.Value) continue;
                cursorHasMoreData = true;
                break;
            }

            results["columns"] = columns;
            results["rows"] = rows;

            return results;
        }

        private void CloseCursor(EditorSqliteCursor sqliteCursor)
        {
            try
            {
                int cursorId = sqliteCursor.cursorId;
                if (LogLevel.HasVerboseLevel(logLevel))
                {
                    Debug.Log($"{GetThreadLogPrefix()}closing cursor {cursorId}");
                }

                cursors.Remove(cursorId);
                sqliteCursor.reader?.Close();
                sqliteCursor.reader?.Dispose();
                sqliteCursor.command?.Dispose();
            }
            catch (Exception e)
            {
                Debug.LogError($"[{GetThreadLogTag()}] Error closing cursor: {e.Message}");
            }
        }

        private void CloseCursor(int cursorId)
        {
            if (cursors.TryGetValue(cursorId, out EditorSqliteCursor sqliteCursor))
            {
                CloseCursor(sqliteCursor);
            }
        }

        #endregion

        #region Transaction Management

        private void RunQueuedOperations()
        {
            while (noTransactionOperationQueue.Count > 0)
            {
                if (currentTransactionId.HasValue)
                {
                    break;
                }

                QueuedOperation queuedOperation = noTransactionOperationQueue[0];
                queuedOperation.Run();
                noTransactionOperationQueue.RemoveAt(0);
            }
        }

        private void WrapSqlOperationHandler(IOperation operation, Action action)
        {
            var transactionId = operation.GetTransactionId();

            if (!currentTransactionId.HasValue)
            {
                // No transaction in progress, execute directly
                action();

                // Run queued operations if any and no transaction started
                if (!currentTransactionId.HasValue && noTransactionOperationQueue.Count > 0)
                {
                    databaseWorkerPool?.Post(this, RunQueuedOperations);
                }
            }
            else if (transactionId.HasValue && (transactionId.Value == currentTransactionId.Value ||
                                                transactionId.Value ==
                                                GuruSqliteConstants.ParamTransactionIdValueForce))
            {
                // Operation matches current transaction or forces execution
                action();

                // Run queued operations if transaction ended
                if (!currentTransactionId.HasValue && noTransactionOperationQueue.Count > 0)
                {
                    databaseWorkerPool?.Post(this, RunQueuedOperations);
                }
            }
            else
            {
                // Queue for later execution
                QueuedOperation queuedOperation = new QueuedOperation(operation, action);
                noTransactionOperationQueue.Add(queuedOperation);
            }
        }

        public bool IsInTransaction()
        {
            return transactionDepth > 0;
        }

        public void EnterOrLeaveInTransaction(bool? value)
        {
            if (value == true)
            {
                transactionDepth++;
            }
            else if (value == false)
            {
                transactionDepth--;
            }
        }

        #endregion

        #region SQL Execution

        private void HandleException(Exception exception, IOperation operation)
        {
            if (exception is SqliteException sqliteException)
            {
                if (sqliteException.Message.Contains("unable to open database"))
                {
                    operation.Error(GuruSqliteConstants.SqliteErrorCode,
                        GuruSqliteConstants.MethodOpenDatabase + " " + path, null);
                    return;
                }

                operation.Error(GuruSqliteConstants.SqliteErrorCode, exception.Message, SqlErrorInfo.GetMap(operation));
                SqliteLogger.Log("handleException1:" + exception.Message);
                SqliteLogger.LogException(exception);
                return;
            }

            operation.Error(GuruSqliteConstants.SqliteErrorCode, exception.Message, SqlErrorInfo.GetMap(operation));
            SqliteLogger.Log("handleException2:" + exception.Message);
            SqliteLogger.LogException(exception);
        }

        private bool ExecuteOrError(IOperation operation)
        {
            SqlCommand command = operation.GetSqlCommand();
            if (LogLevel.HasSqlLevel(logLevel))
            {
                SqliteLogger.Log(GetThreadLogPrefix() + command);
            }

            bool? operationInTransaction = operation.GetInTransactionChange();

            try
            {
                var sql = command.Sql;
                var sqlArguments = command.Arguments ?? Array.Empty<object>();

                SqliteLogger.Log($"executeOrError: {sql} {string.Join(", ", sqlArguments)}");

                using (var dbCommand = sqliteConnection.CreateCommand())
                {
                    dbCommand.CommandText = sql;

                    // Add parameters
                    for (var i = 0; i < sqlArguments.Length; i++)
                    {
                        var parameter = dbCommand.CreateParameter();
                        parameter.ParameterName = $"@p{i}";
                        parameter.Value = sqlArguments[i] ?? DBNull.Value;
                        dbCommand.Parameters.Add(parameter);
                    }

                    // Replace ? with named parameters
                    dbCommand.CommandText = ReplaceQuestionMarks(dbCommand.CommandText);

                    dbCommand.ExecuteNonQuery();
                }

                EnterOrLeaveInTransaction(operationInTransaction);
                return true;
            }
            catch (Exception exception)
            {
                HandleException(exception, operation);
                return false;
            }
        }

        private string ReplaceQuestionMarks(string sql)
        {
            string result = sql;
            int paramIndex = 0;

            while (result.Contains("?"))
            {
                result = result.ReplaceFirst("?", $"@p{paramIndex}");
                paramIndex++;
            }

            return result;
        }

        #endregion

        #region Public API Methods

        public void Query(IOperation operation)
        {
            WrapSqlOperationHandler(operation, () => DoQuery(operation));
        }

        private bool DoQuery(IOperation operation)
        {
            // Non null means dealing with saved cursor.
            var cursorPageSize = operation.GetArgument<int?>(GuruSqliteConstants.ParamCursorPageSize);
            var cursorHasMoreData = false;

            SqlCommand command = operation.GetSqlCommand();
            EditorSqliteCursor editorCursor = null;

            if (LogLevel.HasSqlLevel(logLevel))
            {
                Debug.Log(GetThreadLogPrefix() + command);
            }

            try
            {
                var dbCommand = sqliteConnection.CreateCommand();
                dbCommand.CommandText = command.Sql;

                var args = command.Arguments ?? Array.Empty<object>();
                // Add parameters
                for (var i = 0; i < args.Length; i++)
                {
                    var parameter = dbCommand.CreateParameter();
                    parameter.ParameterName = $"@p{i}";
                    parameter.Value = args[i] ?? DBNull.Value;
                    dbCommand.Parameters.Add(parameter);
                }

                // Replace ? with named parameters
                dbCommand.CommandText = ReplaceQuestionMarks(dbCommand.CommandText);

                var reader = dbCommand.ExecuteReader();

                var results = CursorToResults(reader, cursorPageSize);

                if (cursorPageSize.HasValue)
                {
                    // Check if reader has more rows
                    cursorHasMoreData = !reader.IsClosed && reader.Read();

                    // If we've read a row to check for more data, move back
                    if (cursorHasMoreData && reader.HasRows)
                    {
                        // We'll have potentially more data to fetch
                        var cursorId = ++lastCursorId;
                        results["cursor_id"] = cursorId;
                        editorCursor = new EditorSqliteCursor(cursorId, cursorPageSize.Value, reader, dbCommand);
                        cursors[cursorId] = editorCursor;
                    }
                }

                operation.Success(results);
                return true;
            }
            catch (Exception exception)
            {
                HandleException(exception, operation);
                // Cleanup
                if (editorCursor != null)
                {
                    CloseCursor(editorCursor);
                }

                return false;
            }
            finally
            {
                // Close the cursor for non-paged query
                if (editorCursor == null)
                {
                    // The reader will be closed when disposed
                }
            }
        }

        public void QueryCursorNext(IOperation operation)
        {
            WrapSqlOperationHandler(operation, () => DoQueryCursorNext(operation));
        }

        private bool DoQueryCursorNext(IOperation operation)
        {
            var cursorId = operation.GetArgument<int>(GuruSqliteConstants.ParamCursorId);
            var cancel = operation.GetArgument<bool>(GuruSqliteConstants.ParamCursorCancel);

            if (LogLevel.HasVerboseLevel(logLevel))
            {
                Debug.Log($"{GetThreadLogPrefix()}cursor {cursorId} {(cancel ? "cancel" : "next")}");
            }

            if (cancel)
            {
                CloseCursor(cursorId);
                operation.Success(null);
                return true;
            }

            if (!cursors.TryGetValue(cursorId, out var sqliteCursor))
            {
                operation.Error(GuruSqliteConstants.SqliteErrorCode, $"Cursor {cursorId} not found", null);
                return false;
            }

            var cursorHasMoreData = false;

            try
            {
                var results = CursorToResults(sqliteCursor.reader, sqliteCursor.pageSize);

                // Check if we have more data
                cursorHasMoreData = !sqliteCursor.reader.IsClosed && sqliteCursor.reader.Read();

                if (cursorHasMoreData)
                {
                    // Keep the cursor Id in the response to specify that we have more data
                    results["cursor_id"] = cursorId;
                }

                operation.Success(results);
                return true;
            }
            catch (Exception exception)
            {
                HandleException(exception, operation);
                // Cleanup
                CloseCursor(sqliteCursor);
                return false;
            }
            finally
            {
                // Close the cursor if we don't have any more data
                if (!cursorHasMoreData)
                {
                    CloseCursor(sqliteCursor);
                }
            }
        }

        public void Execute(IOperation operation)
        {
            WrapSqlOperationHandler(operation, () =>
            {
                bool? inTransactionChange = operation.GetInTransactionChange();
                // Transaction v2 support
                bool enteringTransaction = inTransactionChange == true && operation.HasNullTransactionId();

                if (enteringTransaction)
                {
                    currentTransactionId = ++lastTransactionId;
                }

                if (!ExecuteOrError(operation))
                {
                    // Revert if needed
                    if (enteringTransaction)
                    {
                        currentTransactionId = null;
                    }
                }
                else if (enteringTransaction)
                {
                    // Return the transaction id
                    var result = new Dictionary<string, object>
                    {
                        [GuruSqliteConstants.ParamTransactionId] = currentTransactionId.Value
                    };
                    operation.Success(result);
                }
                else
                {
                    if (inTransactionChange == false)
                    {
                        // We are leaving our current transaction
                        currentTransactionId = null;
                    }

                    operation.Success(null);
                }
            });
        }

        private bool DoExecute(IOperation operation)
        {
            if (!ExecuteOrError(operation))
            {
                return false;
            }

            operation.Success(null);
            return true;
        }

        public void Insert(IOperation operation)
        {
            WrapSqlOperationHandler(operation, () => DoInsert(operation));
        }

        private bool DoInsert(IOperation operation)
        {
            if (!ExecuteOrError(operation))
            {
                return false;
            }

            // don't get last id if not expected
            if (operation.GetNoResult())
            {
                operation.Success(null);
                return true;
            }

            try
            {
                using (var command = sqliteConnection.CreateCommand())
                {
                    // Read both the changes and last insert row id in one sql call
                    command.CommandText = "SELECT changes(), last_insert_rowid()";
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            int changed = reader.GetInt32(0);

                            // If the change count is 0, assume the insert failed and return null
                            if (changed == 0)
                            {
                                long id = reader.GetInt64(1);
                                if (LogLevel.HasSqlLevel(logLevel))
                                {
                                    Debug.Log($"{GetThreadLogPrefix()}no changes (id was {id})");
                                }

                                operation.Success(null);
                            }
                            else
                            {
                                long id = reader.GetInt64(1);
                                if (LogLevel.HasSqlLevel(logLevel))
                                {
                                    Debug.Log($"{GetThreadLogPrefix()}inserted {id}");
                                }

                                operation.Success(id);
                            }

                            return true;
                        }
                        else
                        {
                            Debug.LogError($"{GetThreadLogPrefix()}fail to read changes for Insert");
                        }
                    }
                }

                operation.Success(null);
                return true;
            }
            catch (Exception exception)
            {
                HandleException(exception, operation);
                return false;
            }
        }

        public void Update(IOperation operation)
        {
            WrapSqlOperationHandler(operation, () => DoUpdate(operation));
        }

        private bool DoUpdate(IOperation operation)
        {
            if (!ExecuteOrError(operation))
            {
                return false;
            }

            // don't get last id if not expected
            if (operation.GetNoResult())
            {
                operation.Success(null);
                return true;
            }

            try
            {
                using (var command = sqliteConnection.CreateCommand())
                {
                    command.CommandText = "SELECT changes()";

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            int changed = reader.GetInt32(0);
                            if (LogLevel.HasSqlLevel(logLevel))
                            {
                                Debug.Log($"{GetThreadLogPrefix()}changed {changed}");
                            }

                            operation.Success(changed);
                            return true;
                        }
                        else
                        {
                            Debug.LogError($"{GetThreadLogPrefix()}fail to read changes for Update/Delete");
                        }
                    }
                }

                operation.Success(null);
                return true;
            }
            catch (Exception exception)
            {
                HandleException(exception, operation);
                return false;
            }
        }

        public void Batch(MethodCall call, SqliteResult result)
        {
            SqliteLogger.Log("enter batch");
            MethodCallOperation mainOperation = new MethodCallOperation(call, result);

            var noResult = mainOperation.GetNoResult();
            var continueOnError = mainOperation.GetContinueOnError();

            var operations =
                mainOperation.GetArgument<List<Dictionary<string, object>>>(GuruSqliteConstants.ParamOperations);
            var results = new List<Dictionary<string, object>>();

            foreach (var map in operations)
            {
                EditorBatchOperation operation = new EditorBatchOperation(map, noResult);
                string method = "unknown";

                try
                {
                    method = operation.GetMethod();
                }
                catch (Exception exception)
                {
                    SqliteLogger.LogException(exception);
                }

                SqliteLogger.Log("batch method:" + method);

                switch (method)
                {
                    case GuruSqliteConstants.MethodExecute:
                        if (DoExecute(operation))
                        {
                            operation.HandleSuccess(results);
                        }
                        else if (continueOnError)
                        {
                            operation.HandleErrorContinue(results);
                        }
                        else
                        {
                            operation.HandleError(result);
                            return;
                        }

                        break;
                    case GuruSqliteConstants.MethodInsert:
                        if (DoInsert(operation))
                        {
                            operation.HandleSuccess(results);
                        }
                        else if (continueOnError)
                        {
                            operation.HandleErrorContinue(results);
                        }
                        else
                        {
                            operation.HandleError(result);
                            return;
                        }

                        break;
                    case GuruSqliteConstants.MethodQuery:
                        if (DoQuery(operation))
                        {
                            operation.HandleSuccess(results);
                        }
                        else if (continueOnError)
                        {
                            operation.HandleErrorContinue(results);
                        }
                        else
                        {
                            operation.HandleError(result);
                            return;
                        }

                        break;
                    case GuruSqliteConstants.MethodUpdate:
                        if (DoUpdate(operation))
                        {
                            operation.HandleSuccess(results);
                        }
                        else if (continueOnError)
                        {
                            operation.HandleErrorContinue(results);
                        }
                        else
                        {
                            operation.HandleError(result);
                            return;
                        }

                        break;
                    default:
                        result.Error("BadParams", "Batch method '" + method + "' not supported", null);
                        return;
                }
            }

            // Set the results of all operations
            if (noResult)
            {
                result.Success(null);
            }
            else
            {
                result.Success(results);
            }
        }

        #endregion
    }

    #region Helper Classes

    public class EditorSqliteCursor
    {
        public readonly int cursorId;
        public readonly int pageSize;
        public readonly SqliteDataReader reader;
        public readonly SqliteCommand command;

        public EditorSqliteCursor(int cursorId, int pageSize, SqliteDataReader reader, SqliteCommand command)
        {
            this.cursorId = cursorId;
            this.pageSize = pageSize;
            this.reader = reader;
            this.command = command;
        }
    }

    public class QueuedOperation
    {
        private readonly IOperation operation;
        private readonly Action action;

        public QueuedOperation(IOperation operation, Action action)
        {
            this.operation = operation;
            this.action = action;
        }

        public void Run()
        {
            action();
        }
    }

    public static class StringExtensions
    {
        public static string ReplaceFirst(this string text, string search, string replace)
        {
            int pos = text.IndexOf(search);
            if (pos < 0)
            {
                return text;
            }

            return text.Substring(0, pos) + replace + text.Substring(pos + search.Length);
        }
    }

    public interface IDatabaseWorkerPool
    {
        void Post(EditorDatabase database, Action action);
    }


    public static class LogLevel
    {
        public const int NONE = 0;
        public const int SQL = 1;
        public const int VERBOSE = 2;

        public static bool HasSqlLevel(int level)
        {
            return level >= SQL;
        }

        public static bool HasVerboseLevel(int level)
        {
            return level >= VERBOSE;
        }
    }

    public static class SqliteLogger
    {
        public static void Log(string message)
        {
            Debug.Log("[SQLite] " + message);
        }

        public static void LogException(Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    public static class SqlErrorInfo
    {
        public static object GetMap(IOperation operation)
        {
            var map = new Dictionary<string, object>();

            try
            {
                SqlCommand command = operation.GetSqlCommand();
                map["sql"] = command.Sql;
                map["arguments"] = command.Arguments ?? Array.Empty<object>();
            }
            catch (Exception)
            {
                // Ignore exceptions when collecting error info
            }

            return map;
        }
    }
}

#endregion
#endif