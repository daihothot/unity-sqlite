#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace GuruSqlite
{
    public class EditorSqliteExecutor
    {
        /// <summary>
    /// EditorDatabaseExecutor 负责管理数据库连接并执行操作
    /// 相当于 Android 端 DatabaseExecutor 的实现
    /// </summary>
    public class EditorDatabaseExecutor : IDatabaseWorkerPool
    {
        private static readonly string TAG = "EditorDatabaseExecutor";
        
        // Constants
        private const int DEFAULT_LOG_LEVEL = LogLevel.NONE;
        
        // Database storage
        private readonly Dictionary<int, EditorDatabase> databaseMap = new Dictionary<int, EditorDatabase>();
        private int lastDatabaseId = 0;
        
        // Log level
        private int logLevel = DEFAULT_LOG_LEVEL;
        
        /// <summary>
        /// 设置日志级别
        /// </summary>
        /// <param name="logLevel">日志级别</param>
        public void SetLogLevel(int logLevel)
        {
            this.logLevel = logLevel;
        }
        
        /// <summary>
        /// 打开数据库
        /// </summary>
        public void OpenDatabase(MethodCall methodCall, SqliteResult result)
        {
            var path = methodCall.GetArgument<string>("path");
            var readOnly = methodCall.GetArgument<bool>("read_only");
            var singleInstance = methodCall.GetArgument<bool>("single_instance");
            
            try
            {
                if (path == null)
                {
                    result.Error(GuruSqliteConstants.ErrorBadParam, "path cannot be null", null);
                    return;
                }
                
                // Resolve database path (handle relative paths)
                path = ResolveDatabasePath(path);
                
                // Look for existing instance if single-instance is true
                var databaseId = 0;
                
                if (singleInstance)
                {
                    databaseId = FindDatabaseId(path);
                    
                    if (databaseId > 0)
                    {
                        // Database already open
                        if (LogLevel.HasVerboseLevel(logLevel))
                        {
                            Debug.Log($"[{TAG}] database {path} already opened with id {databaseId}");
                        }
                        
                        result.Success(new Dictionary<string, object> { ["id"] = databaseId });
                        return;
                    }
                }
                
                // Create database instance
                databaseId = ++lastDatabaseId;
                EditorDatabase database = new EditorDatabase(path, databaseId, singleInstance, logLevel);
                database.databaseWorkerPool = this;
                
                // Open database connection
                try
                {
                    if (readOnly)
                    {
                        if (!File.Exists(path))
                        {
                            result.Error(GuruSqliteConstants.SqliteErrorCode, GuruSqliteConstants.ErrorOpenFailed + " " + path, null);
                            return;
                        }
                        database.OpenReadOnly();
                    }
                    else
                    {
                        database.Open();
                    }
                    
                    // Add to map
                    databaseMap[databaseId] = database;
                    
                    if (LogLevel.HasVerboseLevel(logLevel))
                    {
                        Debug.Log($"[{TAG}] opened database {path} with id {databaseId}");
                    }
                    
                    result.Success(new Dictionary<string, object> { ["id"] = databaseId });
                }
                catch (Exception e)
                {
                    result.Error(GuruSqliteConstants.SqliteErrorCode, e.Message, null);
                }
            }
            catch (Exception e)
            {
                result.Error(GuruSqliteConstants.SqliteErrorCode, e.Message, null);
            }
        }
        
        /// <summary>
        /// 关闭数据库
        /// </summary>
        public void CloseDatabase(MethodCall methodCall, SqliteResult result)
        {
            int databaseId = methodCall.GetArgument<int>("id");
            bool force = methodCall.GetArgument<bool>("force");
            
            try
            {
                EditorDatabase database = GetDatabase(databaseId);
                
                if (database == null)
                {
                    if (LogLevel.HasVerboseLevel(logLevel))
                    {
                        Debug.Log($"[{TAG}] cannot close database {databaseId} (not found)");
                    }
                    result.Success(null);
                    return;
                }
                
                if (!force && database.IsInTransaction())
                {
                    result.Error(GuruSqliteConstants.SqliteErrorCode, "database in transaction", null);
                    return;
                }
                
                try
                {
                    database.Close();
                    databaseMap.Remove(databaseId);
                    
                    if (LogLevel.HasVerboseLevel(logLevel))
                    {
                        Debug.Log($"[{TAG}] closed database {databaseId}");
                    }
                    
                    result.Success(null);
                }
                catch (Exception e)
                {
                    result.Error(GuruSqliteConstants.SqliteErrorCode, e.Message, null);
                }
            }
            catch (Exception e)
            {
                result.Error(GuruSqliteConstants.SqliteErrorCode, e.Message, null);
            }
        }
        
        /// <summary>
        /// 删除数据库
        /// </summary>
        public void DeleteDatabase(MethodCall methodCall, SqliteResult result)
        {
            string path = methodCall.GetArgument<string>("path");
            
            try
            {
                if (path == null)
                {
                    result.Error(GuruSqliteConstants.ErrorBadParam, "path cannot be null", null);
                    return;
                }
                
                // Resolve database path
                path = ResolveDatabasePath(path);
                
                // Close any open database with the same path
                int databaseId = FindDatabaseId(path);
                if (databaseId > 0)
                {
                    EditorDatabase database = GetDatabase(databaseId);
                    database.Close();
                    databaseMap.Remove(databaseId);
                    
                    if (LogLevel.HasVerboseLevel(logLevel))
                    {
                        Debug.Log($"[{TAG}] closed database {databaseId} for deletion");
                    }
                }
                
                // Delete the database file
                EditorDatabase.DeleteDatabase(path);
                
                if (LogLevel.HasVerboseLevel(logLevel))
                {
                    Debug.Log($"[{TAG}] deleted database {path}");
                }
                
                result.Success(null);
            }
            catch (Exception e)
            {
                result.Error(GuruSqliteConstants.SqliteErrorCode, e.Message, null);
            }
        }
        
        /// <summary>
        /// 执行数据库操作
        /// </summary>
        public void Execute(MethodCall methodCall, SqliteResult result)
        {
            int databaseId = methodCall.GetArgument<int>("id");
            
            try
            {
                EditorDatabase database = GetDatabase(databaseId);
                
                if (database == null)
                {
                    result.Error(GuruSqliteConstants.SqliteErrorCode, "database not found", null);
                    return;
                }
                
                if (LogLevel.HasVerboseLevel(logLevel))
                {
                    Debug.Log($"[{TAG}] execute on database {databaseId}");
                }
                
                MethodCallOperation operation = new MethodCallOperation(methodCall, result);
                database.Execute(operation);
            }
            catch (Exception e)
            {
                result.Error(GuruSqliteConstants.SqliteErrorCode, e.Message, null);
            }
        }
        
        /// <summary>
        /// 执行查询操作
        /// </summary>
        public void Query(MethodCall methodCall, SqliteResult result)
        {
            int databaseId = methodCall.GetArgument<int>("id");
            
            try
            {
                EditorDatabase database = GetDatabase(databaseId);
                
                if (database == null)
                {
                    result.Error(GuruSqliteConstants.SqliteErrorCode, "database not found", null);
                    return;
                }
                
                if (LogLevel.HasVerboseLevel(logLevel))
                {
                    Debug.Log($"[{TAG}] query on database {databaseId}");
                }
                
                MethodCallOperation operation = new MethodCallOperation(methodCall, result);
                database.Query(operation);
            }
            catch (Exception e)
            {
                result.Error(GuruSqliteConstants.SqliteErrorCode, e.Message, null);
            }
        }
        
        /// <summary>
        /// 获取下一批查询结果
        /// </summary>
        public void QueryCursorNext(MethodCall methodCall, SqliteResult result)
        {
            int databaseId = methodCall.GetArgument<int>("id");
            
            try
            {
                EditorDatabase database = GetDatabase(databaseId);
                
                if (database == null)
                {
                    result.Error(GuruSqliteConstants.SqliteErrorCode, "database not found", null);
                    return;
                }
                
                if (LogLevel.HasVerboseLevel(logLevel))
                {
                    Debug.Log($"[{TAG}] query cursor next on database {databaseId}");
                }
                
                MethodCallOperation operation = new MethodCallOperation(methodCall, result);
                database.QueryCursorNext(operation);
            }
            catch (Exception e)
            {
                result.Error(GuruSqliteConstants.SqliteErrorCode, e.Message, null);
            }
        }
        
        /// <summary>
        /// 执行插入操作
        /// </summary>
        public void Insert(MethodCall methodCall, SqliteResult result)
        {
            int databaseId = methodCall.GetArgument<int>("id");
            
            try
            {
                EditorDatabase database = GetDatabase(databaseId);
                
                if (database == null)
                {
                    result.Error(GuruSqliteConstants.SqliteErrorCode, "database not found", null);
                    return;
                }
                
                if (LogLevel.HasVerboseLevel(logLevel))
                {
                    Debug.Log($"[{TAG}] insert on database {databaseId}");
                }
                
                MethodCallOperation operation = new MethodCallOperation(methodCall, result);
                database.Insert(operation);
            }
            catch (Exception e)
            {
                result.Error(GuruSqliteConstants.SqliteErrorCode, e.Message, null);
            }
        }
        
        /// <summary>
        /// 执行更新操作
        /// </summary>
        public void Update(MethodCall methodCall, SqliteResult result)
        {
            int databaseId = methodCall.GetArgument<int>("id");
            
            try
            {
                EditorDatabase database = GetDatabase(databaseId);
                
                if (database == null)
                {
                    result.Error(GuruSqliteConstants.SqliteErrorCode, "database not found", null);
                    return;
                }
                
                if (LogLevel.HasVerboseLevel(logLevel))
                {
                    Debug.Log($"[{TAG}] update on database {databaseId}");
                }
                
                MethodCallOperation operation = new MethodCallOperation(methodCall, result);
                database.Update(operation);
            }
            catch (Exception e)
            {
                result.Error(GuruSqliteConstants.SqliteErrorCode, e.Message, null);
            }
        }
        
        /// <summary>
        /// 执行批量操作
        /// </summary>
        public void Batch(MethodCall methodCall, SqliteResult result)
        {
            int databaseId = methodCall.GetArgument<int>("id");
            
            try
            {
                EditorDatabase database = GetDatabase(databaseId);
                
                if (database == null)
                {
                    result.Error(GuruSqliteConstants.SqliteErrorCode, "database not found", null);
                    return;
                }
                
                if (LogLevel.HasVerboseLevel(logLevel))
                {
                    Debug.Log($"[{TAG}] batch on database {databaseId}");
                }
                
                database.Batch(methodCall, result);
            }
            catch (Exception e)
            {
                result.Error(GuruSqliteConstants.SqliteErrorCode, e.Message, null);
            }
        }
        
        /// <summary>
        /// 检查数据库是否存在
        /// </summary>
        public void DatabaseExists(MethodCall methodCall, SqliteResult result)
        {
            string path = methodCall.GetArgument<string>("path");
            
            try
            {
                if (path == null)
                {
                    result.Error("badParams", "path cannot be null", null);
                    return;
                }
                
                // Resolve database path
                path = ResolveDatabasePath(path);
                
                bool exists = EditorDatabase.ExistsDatabase(path);
                
                if (LogLevel.HasVerboseLevel(logLevel))
                {
                    Debug.Log($"[{TAG}] database {path} exists? {exists}");
                }
                
                result.Success(exists);
            }
            catch (Exception e)
            {
                result.Error(GuruSqliteConstants.SqliteErrorCode, e.Message, null);
            }
        }
        
        /// <summary>
        /// 查找已打开的数据库 ID
        /// </summary>
        private int FindDatabaseId(string path)
        {
            return (from entry in databaseMap where entry.Value.path.Equals(path, StringComparison.OrdinalIgnoreCase) select entry.Key).FirstOrDefault();
        }
        
        /// <summary>
        /// 根据 ID 获取数据库实例
        /// </summary>
        private EditorDatabase GetDatabase(int databaseId)
        {
            if (databaseMap.TryGetValue(databaseId, out EditorDatabase database))
            {
                return database;
            }
            
            return null;
        }
        
        /// <summary>
        /// 解析数据库路径
        /// </summary>
        private string ResolveDatabasePath(string path)
        {
            // 如果是相对路径，则转换为绝对路径
            return !Path.IsPathRooted(path) ?
                // 使用 Application.persistentDataPath 作为基础路径
                Path.Combine(Application.persistentDataPath, path) : path;
        }
        
        #region IDatabaseWorkerPool

        /// <summary>
        /// 在线程池中执行操作
        /// </summary>
        public void Post(EditorDatabase database, Action action)
        {
            // 使用 ThreadPool 异步执行任务
            ThreadPool.QueueUserWorkItem(_ => {
                try
                {
                    action?.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[{TAG}] Error executing database operation: {e.Message}");
                    Debug.LogException(e);
                }
            });
        }

        #endregion
    }
    }
}
#endif