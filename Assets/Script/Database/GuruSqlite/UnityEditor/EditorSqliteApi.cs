#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace GuruSqlite
{
    /// <summary>
    /// Unity Editor SQLite 插件
    /// 作为插件的入口点，负责处理来自 Unity 的 SQLite 相关请求
    /// </summary>
    public class EditorSqliteApi : IGuruSqliteApi
    {
        // 单例实例
        private static EditorSqliteApi instance;

        // 数据库执行器
        private readonly EditorSqliteExecutor.EditorDatabaseExecutor databaseExecutor;

        // 方法字典，用于映射方法名称到处理方法
        private readonly Dictionary<string, Action<MethodCall, SqliteResult>> methods;

        // 私有构造函数
        internal EditorSqliteApi()
        {
            databaseExecutor = new EditorSqliteExecutor.EditorDatabaseExecutor();
            methods = new Dictionary<string, Action<MethodCall, SqliteResult>>();

            // 注册方法处理器
            RegisterMethods();
        }

        /// <summary>
        /// 获取插件实例
        /// </summary>
        public static EditorSqliteApi GetInstance()
        {
            if (instance == null)
            {
                instance = new EditorSqliteApi();
            }

            return instance;
        }

        /// <summary>
        /// 注册方法处理器
        /// </summary>
        private void RegisterMethods()
        {
            // 数据库操作
            methods["openDatabase"] = databaseExecutor.OpenDatabase;
            methods["closeDatabase"] = databaseExecutor.CloseDatabase;
            methods["deleteDatabase"] = databaseExecutor.DeleteDatabase;
            methods["databaseExists"] = databaseExecutor.DatabaseExists;

            // SQL 操作
            methods["execute"] = databaseExecutor.Execute;
            methods["insert"] = databaseExecutor.Insert;
            methods["update"] = databaseExecutor.Update;
            methods["query"] = databaseExecutor.Query;
            methods["batch"] = databaseExecutor.Batch;

            // 游标操作
            methods["queryCursorNext"] = databaseExecutor.QueryCursorNext;

            // 其他方法
            methods["debug"] = Debug;
            methods["setLogLevel"] = SetLogLevel;
        }

        /// <summary>
        /// 调用方法
        /// </summary>
        public void Call(MethodCall methodCall, SqliteResult result)
        {
            string method = methodCall.Method;

            if (method == null)
            {
                result.Error(GuruSqliteConstants.ErrorBadParam, "method is null", null);
                return;
            }

            if (methods.TryGetValue(method, out var handler))
            {
                try
                {
                    handler(methodCall, result);
                }
                catch (Exception e)
                {
                    result.Error(GuruSqliteConstants.SqliteErrorCode, e.Message, null);
                }
            }
            else
            {
                result.NotImplemented();
            }
        }

        /// <summary>
        /// 设置日志级别
        /// </summary>
        private void SetLogLevel(MethodCall methodCall, SqliteResult result)
        {
            var logLevel = methodCall.GetArgument<int>("logLevel");

            try
            {
                // 设置数据库执行器的日志级别
                databaseExecutor.SetLogLevel(logLevel);
                result.Success(null);
            }
            catch (Exception e)
            {
                result.Error(GuruSqliteConstants.SqliteErrorCode, e.Message, null);
            }
        }

        /// <summary>
        /// 调试方法
        /// </summary>
        private void Debug(MethodCall methodCall, SqliteResult result)
        {
            try
            {
                var enabled = methodCall.GetArgument<bool>("on");

                // 根据需要记录调试信息
                UnityEngine.Debug.Log($"[EditorSqlitePlugin] Debug mode: {enabled}");

                result.Success(null);
            }
            catch (Exception e)
            {
                result.Error(GuruSqliteConstants.SqliteErrorCode, e.Message, null);
            }
        }

        /// <summary>
        /// 注册自定义异步结果回调
        /// </summary>
        /// <param name="methodCall">方法调用</param>
        /// <returns>异步操作结果</returns>
        public async UniTask<T> InvokeMethod<T>(string method, object arguments)
        {
            var methodCall = new MethodCall(method, arguments);
            var result = new EditorSqliteResult<T>();

            Call(methodCall, result);

            return await result.Tcs.Task;
        }
    }
}

#endif