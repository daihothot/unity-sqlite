#nullable enable
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace GuruSqlite
{

    /// <summary>
    /// 数据库工厂接口
    /// </summary>
    public interface IDatabaseFactory
    {
        /// <summary>
        /// 打开指定路径的数据库
        /// </summary>
        UniTask<SqliteDatabase> OpenDatabase(string path, OpenDatabaseOptions? options = null);

        /// <summary>
        /// 获取默认数据库路径
        /// </summary>
        UniTask<string> GetDatabasesPath();

        /// <summary>
        /// 设置默认数据库路径
        /// </summary>
        UniTask SetDatabasesPath(string path);

        /// <summary>
        /// 删除指定路径的数据库
        /// </summary>
        UniTask DeleteDatabase(string path);

        /// <summary>
        /// 检查指定路径的数据库是否存在
        /// </summary>
        UniTask<bool> DatabaseExists(string path);

        /// <summary>
        /// 写入数据库字节
        /// </summary>
        UniTask WriteDatabaseBytes(string path, byte[] bytes);

        /// <summary>
        /// 读取数据库字节
        /// </summary>
        UniTask<byte[]> ReadDatabaseBytes(string path);
    }

    /// <summary>
    /// 数据库执行器接口
    /// </summary>
    public interface IDatabaseExecutor
    {
        /// <summary>
        /// 执行SQL命令，无返回值
        /// </summary>
        UniTask Execute(string sql, object[]? arguments = null);

        /// <summary>
        /// 执行原始INSERT SQL命令，返回最后插入的行ID
        /// </summary>
        UniTask<long> RawInsert(string sql, object[]? arguments = null);

        /// <summary>
        /// 向指定表插入值，返回最后插入的行ID
        /// </summary>
        UniTask<long> Insert(string table, Dictionary<string, object?> values, string? nullColumnHack = null, ConflictAlgorithm? conflictAlgorithm = null);

        /// <summary>
        /// 执行原始SELECT SQL命令，返回结果集
        /// </summary>
        UniTask<QueryResult> RawQuery(string sql, object[]? arguments = null);

        /// <summary>
        /// 查询指定表，返回结果集
        /// </summary>
        UniTask<QueryResult> Query(
            string table,
            bool? distinct = null,
            string[]? columns = null,
            string? where = null,
            object[]? whereArgs = null,
            string? groupBy = null,
            string? having = null,
            string? orderBy = null,
            int? limit = null,
            int? offset = null);

        /// <summary>
        /// 执行原始UPDATE SQL命令，返回受影响的行数
        /// </summary>
        UniTask<int> RawUpdate(string sql, object[]? arguments = null);

        /// <summary>
        /// 更新指定表中的记录，返回受影响的行数
        /// </summary>
        UniTask<int> Update(
            string table,
            Dictionary<string, object?> values,
            string? where = null,
            object[]? whereArgs = null,
            ConflictAlgorithm? conflictAlgorithm = null);

        /// <summary>
        /// 执行原始DELETE SQL命令，返回受影响的行数
        /// </summary>
        UniTask<int> RawDelete(string sql, object[]? arguments = null);

        /// <summary>
        /// 从指定表中删除记录，返回受影响的行数
        /// </summary>
        UniTask<int> Delete(
            string table,
            string? where = null,
            object[]? whereArgs = null);

        /// <summary>
        /// 创建批处理对象
        /// </summary>
        SqliteBatch Batch();
    }

    /// <summary>
    /// 数据库接口
    /// </summary>
    public interface IDatabase : IDatabaseExecutor
    {
        UniTask<T> Transaction<T>(Func<SqliteTransaction, UniTask<T>> action, bool? exclusive = null);
    }

    /// <summary>
    /// 事务接口
    /// </summary>
    public interface ITransaction : IDatabaseExecutor
    {
        /// <summary>
        /// 提交事务
        /// </summary>
        UniTask Commit();

        /// <summary>
        /// 回滚事务
        /// </summary>
        UniTask Rollback();
    }

    /// <summary>
    /// 批处理接口
    /// </summary>
    public interface IBatch
    {
        UniTask<BatchResults> Commit(bool? exclusive = null,
            bool? noResult = null,
            bool? continueOnError = null);
        
        /// <summary>
        /// 执行SQL命令，无返回值
        /// </summary>
        void Execute(string sql, object[]? arguments = null);

        /// <summary>
        /// 执行原始INSERT SQL命令
        /// </summary>
        void RawInsert(string sql, object[]? arguments = null);

        /// <summary>
        /// 向指定表插入值
        /// </summary>
        void Insert(string table, Dictionary<string, object?> values, string? nullColumnHack = null, ConflictAlgorithm? conflictAlgorithm = null);

        /// <summary>
        /// 执行原始SELECT SQL命令
        /// </summary>
        void RawQuery(string sql, object[]? arguments = null);

        /// <summary>
        /// 查询指定表
        /// </summary>
        void Query(
            string table,
            bool? distinct = null,
            string[]? columns = null,
            string? where = null,
            object[]? whereArgs = null,
            string? groupBy = null,
            string? having = null,
            string? orderBy = null,
            int? limit = null,
            int? offset = null);

        /// <summary>
        /// 执行原始UPDATE SQL命令
        /// </summary>
        void RawUpdate(string sql, object[]? arguments = null);

        /// <summary>
        /// 更新指定表中的记录
        /// </summary>
        void Update(
            string table,
            Dictionary<string, object?> values,
            string? where = null,
            object[]? whereArgs = null,
            ConflictAlgorithm? conflictAlgorithm = null);

        /// <summary>
        /// 执行原始DELETE SQL命令
        /// </summary>
        void RawDelete(string sql, object[]? arguments = null);

        /// <summary>
        /// 从指定表中删除记录
        /// </summary>
        void Delete(
            string table,
            string? where = null,
            object[]? whereArgs = null);

        /// <summary>
        /// 获取批处理操作列表
        /// </summary>
        List<BatchOperation> GetOperations();
    }

    /// <summary>
    /// 批处理操作
    /// </summary>
    public class BatchOperation: SqlCommand
    {
        public string Method { get; }

        public BatchOperation(SqlCommandType type, string method, string sql, object[]? arguments):
            base(type, sql, arguments)
        {
            Method = method;
        }
        
        
        internal Dictionary<string, object> GetOperationParam()
        {
            var result = new Dictionary<string, object>
            {
                { GuruSqliteConstants.ParamMethod, Method },
                { GuruSqliteConstants.ParamSql, Sql }
            };

            if (Arguments != null)
            {
                result[GuruSqliteConstants.ParamSqlArguments] = Arguments;
            }

            if (Type != SqlCommandType.Execute) return result;
            var inTransaction = SqliteUtils.GetSqlInTransactionArgument(Sql);
            if (inTransaction != null)
            {
                result[GuruSqliteConstants.ParamInTransaction] = inTransaction;
            }

            return result;
        }
    }

    /// <summary>
    /// 查询游标接口
    /// </summary>
    public interface IQueryCursor : IDisposable
    {
        /// <summary>
        /// 移动到下一行
        /// </summary>
        UniTask<bool> MoveNext();

        /// <summary>
        /// 获取当前行数据
        /// </summary>
        Dictionary<string, object?> Current { get; }

        /// <summary>
        /// 关闭游标
        /// </summary>
        UniTask Close();
    }

    /// <summary>
    /// 数据库配置回调
    /// </summary>
    public delegate UniTask OnDatabaseConfigureFn(SqliteDatabase db);

    /// <summary>
    /// 数据库创建回调
    /// </summary>
    public delegate UniTask OnDatabaseCreateFn(SqliteDatabase db, int version);

    /// <summary>
    /// 数据库版本变更回调
    /// </summary>
    public delegate UniTask OnDatabaseVersionChangeFn(SqliteDatabase db, int oldVersion, int newVersion);

    /// <summary>
    /// 数据库打开回调
    /// </summary>
    public delegate UniTask OnDatabaseOpenFn(SqliteDatabase db);

    /// <summary>
    /// 打开数据库选项
    /// </summary>
    public class OpenDatabaseOptions
    {
        public int? Version { get; set; }
        public OnDatabaseConfigureFn? OnConfigure { get; set; }
        public OnDatabaseCreateFn? OnCreate { get; set; }
        public OnDatabaseVersionChangeFn? OnUpgrade { get; set; }
        public OnDatabaseVersionChangeFn? OnDowngrade { get; set; }
        public OnDatabaseOpenFn? OnOpen { get; set; }
        public bool ReadOnly { get; set; } = false;
        public bool SingleInstance { get; set; } = true;
    }
} 