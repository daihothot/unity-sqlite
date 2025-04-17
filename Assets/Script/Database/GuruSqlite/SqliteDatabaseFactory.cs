#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GuruSqlite
{


    /// <summary>
    /// SQLite数据库工厂实现
    /// </summary>
    public class SqliteDatabaseFactory : IDatabaseFactory
    {
        private readonly Dictionary<string, SqliteDatabaseOpenHelper> _databaseMap = new Dictionary<string, SqliteDatabaseOpenHelper>();
        private string? _databasesPath;


        private static readonly Lazy<IGuruSqliteApi> SqliteApi = new(() => new IOSSqliteApi()
//             {
// #if UNITY_ANDROID && !UNITY_EDITOR
//             return new AndroidSqliteApi();
// #else
//                  return new MockSqliteApi();
// #endif
//             }
        );
        
        /// <summary>
        /// 打开指定路径的数据库
        /// </summary>
        public async UniTask<SqliteDatabase> OpenDatabase(string path, OpenDatabaseOptions? options = null)
        {
            options ??= new OpenDatabaseOptions();
            
            var absolutePath = await _GetAbsolutePath(path);
            
            // 如果要求单例模式，检查是否已存在实例
            if (options.SingleInstance)
            {
                lock (_databaseMap)
                {
                    if (_databaseMap.TryGetValue(absolutePath, out var helper) && helper.IsOpen)
                    {
                        return helper.Database;
                    }
                }
            }
            
            // 创建新的数据库打开助手
            var databaseHelper = new SqliteDatabaseOpenHelper(this, absolutePath, options);
            var database = await databaseHelper.OpenDatabase();
            
            // 如果是单例模式，保存实例
            if (!options.SingleInstance) return database;
            lock (_databaseMap)
            {
                _databaseMap[absolutePath] = databaseHelper;
            }

            return database;
        }

        /// <summary>
        /// 获取默认数据库路径
        /// </summary>
        public UniTask<string> GetDatabasesPath()
        {
            if (_databasesPath != null) return UniTask.FromResult(_databasesPath);
            _databasesPath = Path.Combine(Application.persistentDataPath, "databases");
            if (!Directory.Exists(_databasesPath))
            {
                Directory.CreateDirectory(_databasesPath);
            }

            return UniTask.FromResult(_databasesPath);
        }

        /// <summary>
        /// 设置默认数据库路径
        /// </summary>
        public UniTask SetDatabasesPath(string path)
        {
            _databasesPath = path;
            if (!Directory.Exists(_databasesPath))
            {
                Directory.CreateDirectory(_databasesPath);
            }
            
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 删除指定路径的数据库
        /// </summary>
        public async UniTask DeleteDatabase(string path)
        {
            var absolutePath = await _GetAbsolutePath(path);
            
            // 如果数据库已打开，先关闭
            SqliteDatabaseOpenHelper? helper = null;
            lock (_databaseMap)
            {
                _databaseMap.Remove(absolutePath, out helper);
            }
            
            if (helper is { IsOpen: true })
            {
                await helper.CloseDatabase();
            }
            
            // 删除文件
            if (File.Exists(absolutePath))
            {
                File.Delete(absolutePath);
            }
            
            // 删除相关的日志文件（如果有）
            var journalPath = absolutePath + "-journal";
            if (File.Exists(journalPath))
            {
                File.Delete(journalPath);
            }
            
            var shmPath = absolutePath + "-shm";
            if (File.Exists(shmPath))
            {
                File.Delete(shmPath);
            }
            
            var walPath = absolutePath + "-wal";
            if (File.Exists(walPath))
            {
                File.Delete(walPath);
            }
        }

        /// <summary>
        /// 检查指定路径的数据库是否存在
        /// </summary>
        public async UniTask<bool> DatabaseExists(string path)
        {
            var absolutePath = await _GetAbsolutePath(path);
            return File.Exists(absolutePath);
        }

        /// <summary>
        /// 写入数据库字节
        /// </summary>
        public async UniTask WriteDatabaseBytes(string path, byte[] bytes)
        {
            var absolutePath = await _GetAbsolutePath(path);
            
            // 确保目录存在
            var directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            // 写入文件
            await File.WriteAllBytesAsync(absolutePath, bytes);
        }

        /// <summary>
        /// 读取数据库字节
        /// </summary>
        public async UniTask<byte[]> ReadDatabaseBytes(string path)
        {
            var absolutePath = await _GetAbsolutePath(path);
            
            if (!File.Exists(absolutePath))
            {
                throw new FileNotFoundException($"Database file not found: {absolutePath}");
            }
            
            return await File.ReadAllBytesAsync(absolutePath);
        }

        /// <summary>
        /// 创建新的数据库实例
        /// </summary>
        internal SqliteDatabase NewDatabase(SqliteDatabaseOpenHelper helper, string path)
        {
            return new SqliteDatabase(this, helper, path);
        }

        /// <summary>
        /// 获取绝对路径
        /// </summary>
        private async UniTask<string> _GetAbsolutePath(string path)
        {
            // 如果是内存数据库，直接返回
            if (path == SqliteConstants.InMemoryDatabasePath)
            {
                return path;
            }
            
            // 如果是绝对路径，直接返回
            if (Path.IsPathRooted(path))
            {
                return path;
            }
            
            // 否则，基于默认数据库路径构建绝对路径
            var databasesPath = await GetDatabasesPath();
            return Path.Combine(databasesPath, path);
        }

        /// <summary>
        /// 调用原生方法
        /// </summary>
        internal UniTask<T?> InvokeMethod<T>(string method, object? arguments)
        {
            return SqliteApi.Value.InvokeMethod<T?>(method, arguments);
        }
    }

    /// <summary>22
    /// 数据库打开助手
    /// </summary>
    public class SqliteDatabaseOpenHelper
    {
        private readonly SqliteDatabaseFactory _factory;
        private readonly string _path;
        private readonly OpenDatabaseOptions _options;
        private SqliteDatabase? _database;

        /// <summary>
        /// 构造函数
        /// </summary>
        public SqliteDatabaseOpenHelper(SqliteDatabaseFactory factory, string path, OpenDatabaseOptions options)
        {
            _factory = factory;
            _path = path;
            _options = options;
        }

        /// <summary>
        /// 数据库是否打开
        /// </summary>
        public bool IsOpen => _database is { IsOpen: true };

        /// <summary>
        /// 获取数据库实例
        /// </summary>
        public SqliteDatabase Database => _database ?? throw new InvalidOperationException("Database is not open");

        /// <summary>
        /// 打开数据库
        /// </summary>
        public async UniTask<SqliteDatabase> OpenDatabase()
        {
            if (IsOpen) return _database;
            var database = _factory.NewDatabase(this, _path);
#if !UNITY_EDITOR
            await database.DoOpen(_options);
#endif
            _database = database;

            return _database;
        }

        /// <summary>
        /// 关闭数据库
        /// </summary>
        public async UniTask CloseDatabase()
        {
            if (IsOpen)
            {
                await _database!.DoClose();
                _database = null;
            }
        }
    }
} 