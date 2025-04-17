#nullable enable
using GuruSqlite;

namespace Guru.SDK.Framework.Utils.Database
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using UnityEngine;
    using Log;
    
    public abstract class AppDatabase
    {
        private SqliteDatabase _database;

        /// <summary>
        /// 表创建器列表
        /// </summary>
        protected abstract List<Func<SqliteTransaction, UniTask<bool>>> TableCreators { get; }


        /// <summary>
        /// 数据库名称
        /// </summary>
        protected abstract string DbName { get; }
        
        protected abstract int Version { get; }
        
        protected abstract List<IMigration> Migrations { get; }

        private void ApplyDatabasePermission(string filePath)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
        try {
            // 调用 java.lang.Runtime 获取运行时对象
            AndroidJavaClass runtimeClass = new AndroidJavaClass("java.lang.Runtime");
            AndroidJavaObject runtime = runtimeClass.CallStatic<AndroidJavaObject>("getRuntime");
            // 构造 chmod 命令，640 权限表示 owner 可读写，group 可读，others 无权限
            string command = "chmod 640 " + filePath;
            // 执行命令
            runtime.Call<AndroidJavaObject>("exec", command);
            Debug.Log("设置数据库文件权限成功: " + filePath);
        } catch (Exception ex) {
            Debug.LogError("修改数据库文件权限失败: " + ex);
        }
#else
#endif
        }
        
        /// <summary>
        /// 初始化数据库路径
        /// </summary>
        private string GetDatabasePath()
        {
            return Path.Combine(GetDatabaseDir(), $"{DbName}.db");
        }
        
        protected virtual string GetDatabaseDir()
        {
            return Application.persistentDataPath;
        }

        /// <summary>
        /// 初始化数据库
        /// </summary>
        public virtual async UniTask<AppDatabase> InitDatabase()
        {
            var dbPath = GetDatabasePath();
            Debug.Log($"InitDatabase dbPath: {dbPath}");
            
            _database = await Sqlite.OpenDatabase(dbPath, Version, onCreate: CreateTables, onUpgrade: Migrate);

            ApplyDatabasePermission(dbPath);
            
            Debug.Log("Database initialization completed.");
            return this;
        }

        /// <summary>
        /// 创建表
        /// </summary>
        private async UniTask CreateTables(SqliteDatabase db, int version)
        { 
            await db.Transaction(async transaction =>
            {
                foreach (var creator in TableCreators)
                {
                    try
                    {
                        await creator(transaction);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Failed to create table: {e}");
                    }
                }

                return true;
            });

            Debug.Log("All tables created.");
        }

        
        private UniTask Migrate(SqliteDatabase database, int from, int to)
        {
            Log.D($"MIGRATE [{from}] => [{to}]");
            var migrations = Migrations;
            return database.Transaction(async txn =>
            {
                var result = MigrateResult.Success;
                for (var index = from - 1; (index < to - 1) && (index < migrations.Count); ++index)
                {

                    try
                    {
                        Log.D($"====> BEGIN MIGRATE [{index + 1}] => [{index + 2}]");
                        result |= await migrations[index].Migrate(txn);
                    }
                    catch (Exception error)
                    {
                        Log.D($"  migrate [{index + 1}] => [{index + 2}] error:[{error}]");
                        throw;
                    }
                    Log.D($"====> END MIGRATE [{index + 1}] => [{index + 2}] result: {result}");
                }

                return result == MigrateResult.Success;
            });
        }
        

        /// <summary>
        /// 备份数据库
        /// </summary>
        public async UniTask Backup(string token)
        {
            var dbPath = GetDatabasePath();
            var backupPath = Path.Combine(Path.GetDirectoryName(dbPath)!, $"{DbName}.{token}.db");
            Debug.Log($"Backing up database to: {backupPath}");

            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }

            await UniTask.RunOnThreadPool(() => File.Copy(dbPath, backupPath));
        }

        /// <summary>
        /// 恢复数据库
        /// </summary>
        public async UniTask<bool> Restore(string token)
        {
            var dbPath = GetDatabasePath();
            var restorePath = Path.Combine(Path.GetDirectoryName(dbPath)!, $"{DbName}.{token}.db");
            Debug.Log($"Restoring database from: {restorePath}");

            if (!File.Exists(restorePath))
            {
                Debug.LogWarning("Restore file not found.");
                return false;
            }

            if (File.Exists(dbPath))
            {
                _database.Close();
                File.Delete(dbPath);
            }

            await UniTask.RunOnThreadPool(() => File.Copy(restorePath, dbPath));
            return true;
        }

        /// <summary>
        /// 切换会话
        /// </summary>
        public async UniTask<bool> SwitchSession(string oldToken, string newToken)
        {
            if (oldToken == newToken)
            {
                Debug.LogWarning("Same token, no need to switch.");
                return false;
            }

            _database.Close();
            if (!string.IsNullOrEmpty(oldToken))
            {
                await Backup(oldToken);
            }

            await Restore(newToken);
            await InitDatabase();
            return true;
        }

        /// <summary>
        /// 重置数据库
        /// </summary>
        public async UniTask<bool> Reset()
        {
            _database.Close();
            var dbPath = GetDatabasePath();
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }

            await InitDatabase();
            return true;
        }

        public SqliteDatabase GetDatabase()
        {
            return _database;
        }

        /// <summary>
        /// 在事务中运行
        /// </summary>
        public UniTask<T> RunInTransaction<T>(Func<SqliteTransaction, UniTask<T>> func)
        {
            return _database.Transaction(func);
        }
    }
}