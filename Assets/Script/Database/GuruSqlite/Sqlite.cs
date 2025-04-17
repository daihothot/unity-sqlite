#nullable enable
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace GuruSqlite
{


    /// <summary>
    /// Sqlite数据库操作的主类
    /// </summary>
    public static class Sqlite
    {
        private static IDatabaseFactory? _databaseFactory;

        /// <summary>
        /// 数据库工厂实例
        /// </summary>
        public static IDatabaseFactory DatabaseFactory
        {
            get => _databaseFactory ??= new SqliteDatabaseFactory();
            set
            {
                _databaseFactory = value;
            }
        }

        /// <summary>
        /// 打开指定路径的数据库
        /// </summary>
        /// <param name="path">数据库路径</param>
        /// <param name="version">数据库版本</param>
        /// <param name="onConfigure">配置回调</param>
        /// <param name="onCreate">创建回调</param>
        /// <param name="onUpgrade">升级回调</param>
        /// <param name="onDowngrade">降级回调</param>
        /// <param name="onOpen">打开回调</param>
        /// <param name="readOnly">是否只读</param>
        /// <param name="singleInstance">是否单例</param>
        /// <returns>数据库实例</returns>
        public static UniTask<SqliteDatabase> OpenDatabase(
            string path,
            int? version = null,
            OnDatabaseConfigureFn? onConfigure = null,
            OnDatabaseCreateFn? onCreate = null,
            OnDatabaseVersionChangeFn? onUpgrade = null,
            OnDatabaseVersionChangeFn? onDowngrade = null,
            OnDatabaseOpenFn? onOpen = null,
            bool readOnly = false,
            bool singleInstance = true)
        {
            var options = new OpenDatabaseOptions
            {
                Version = version,
                OnConfigure = onConfigure,
                OnCreate = onCreate,
                OnUpgrade = onUpgrade,
                OnDowngrade = onDowngrade,
                OnOpen = onOpen,
                ReadOnly = readOnly,
                SingleInstance = singleInstance
            };

            return DatabaseFactory.OpenDatabase(path, options);
        }

        /// <summary>
        /// 以只读模式打开数据库
        /// </summary>
        /// <param name="path">数据库路径</param>
        /// <param name="singleInstance">是否单例</param>
        /// <returns>数据库实例</returns>
        public static UniTask<SqliteDatabase> OpenReadOnlyDatabase(string path, bool singleInstance = true)
        {
            return OpenDatabase(path, readOnly: true, singleInstance: singleInstance);
        }

        /// <summary>
        /// 获取默认数据库路径
        /// </summary>
        public static UniTask<string> GetDatabasesPath()
        {
            return DatabaseFactory.GetDatabasesPath();
        }

        /// <summary>
        /// 删除指定路径的数据库
        /// </summary>
        public static UniTask DeleteDatabase(string path)
        {
            return DatabaseFactory.DeleteDatabase(path);
        }

        /// <summary>
        /// 检查指定路径的数据库是否存在
        /// </summary>
        public static UniTask<bool> DatabaseExists(string path)
        {
            return DatabaseFactory.DatabaseExists(path);
        }
        
        public static bool IsInMemoryDatabasePath(string path)
        {
            return path == SqliteConstants.InMemoryDatabasePath;
        }
    }

    /// <summary>
    /// Sqlite常量
    /// </summary>
    public static class SqliteConstants
    {
        /// <summary>
        /// 内存数据库路径
        /// </summary>
        public const string InMemoryDatabasePath = ":memory:";
        
        /// <summary>
        /// 日志级别：无
        /// </summary>
        public const int LogLevelNone = 0;
        
        /// <summary>
        /// 日志级别：SQL
        /// </summary>
        public const int LogLevelSql = 1;
        
        /// <summary>
        /// 日志级别：详细
        /// </summary>
        public const int LogLevelVerbose = 2;
    }
} 