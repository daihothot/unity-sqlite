#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;

namespace GuruSqlite
{
    /// <summary>
    /// SQLite批处理实现
    /// </summary>
    public abstract class SqliteBatch : IBatch
    {
        private readonly List<BatchOperation> _operations = new();

        public List<Dictionary<string, object>> GetOperationsParam()
        {
            return _operations.Select(batchOperation => batchOperation.GetOperationParam()).ToList();
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        internal SqliteBatch()
        {
        }


        public abstract UniTask<BatchResults> Commit(bool? exclusive = null, bool? noResult = null,
            bool? continueOnError = null);

        /// <summary>
        /// 执行SQL命令
        /// </summary>
        public void Execute(string sql, object[]? arguments = null)
        {
            _operations.Add(new BatchOperation(SqlCommandType.Execute, GuruSqliteConstants.MethodExecute, sql,
                arguments));
        }

        /// <summary>
        /// 执行原始INSERT SQL命令
        /// </summary>
        public void RawInsert(string sql, object[]? arguments = null)
        {
            _operations.Add(new BatchOperation(SqlCommandType.Insert, GuruSqliteConstants.MethodInsert, sql,
                arguments));
        }

        /// <summary>
        /// 向指定表插入值
        /// </summary>
        public void Insert(string table, Dictionary<string, object?> values, string? nullColumnHack = null,
            ConflictAlgorithm? conflictAlgorithm = null)
        {
            if (values.Count == 0 && nullColumnHack == null)
            {
                throw new ArgumentException("Empty values");
            }

            var builder = SqlBuilder.Insert(table, values, nullColumnHack, conflictAlgorithm);

            RawInsert(builder.Sql, builder.Arguments);
        }

        /// <summary>
        /// 执行原始SELECT SQL命令
        /// </summary>
        public void RawQuery(string sql, object[]? arguments = null)
        {
            _operations.Add(new BatchOperation(SqlCommandType.Query, GuruSqliteConstants.MethodQuery, sql, arguments));
        }

        /// <summary>
        /// 查询指定表
        /// </summary>
        public void Query(
            string table,
            bool? distinct = null,
            string[]? columns = null,
            string? where = null,
            object[]? whereArgs = null,
            string? groupBy = null,
            string? having = null,
            string? orderBy = null,
            int? limit = null,
            int? offset = null)
        {
            var builder = SqlBuilder.Query(table, distinct ?? false, columns, where, whereArgs, groupBy, having,
                orderBy, limit, offset);
            RawQuery(builder.Sql, builder.Arguments);
        }

        /// <summary>
        /// 执行原始UPDATE SQL命令
        /// </summary>
        public void RawUpdate(string sql, object[]? arguments = null)
        {
            _operations.Add(new BatchOperation(SqlCommandType.Update, GuruSqliteConstants.MethodUpdate, sql,
                arguments));
        }

        /// <summary>
        /// 更新指定表中的记录
        /// </summary>
        public void Update(
            string table,
            Dictionary<string, object?> values,
            string? where = null,
            object[]? whereArgs = null,
            ConflictAlgorithm? conflictAlgorithm = null)
        {
            if (values.Count == 0)
            {
                throw new System.ArgumentException("Empty values");
            }

            var builder = SqlBuilder.Update(table, values, where, whereArgs, conflictAlgorithm);

            RawUpdate(builder.Sql, builder.Arguments);
        }

        /// <summary>
        /// 执行原始DELETE SQL命令
        /// </summary>
        public void RawDelete(string sql, object[]? arguments = null)
        {
            _operations.Add(new BatchOperation(SqlCommandType.Delete, GuruSqliteConstants.MethodUpdate, sql,
                arguments));
        }

        /// <summary>
        /// 从指定表中删除记录
        /// </summary>
        public void Delete(
            string table,
            string? where = null,
            object[]? whereArgs = null)
        {
            var builder = SqlBuilder.Delete(table, where, whereArgs);
            RawDelete(builder.Sql, builder.Arguments);
        }

        /// <summary>
        /// 获取批处理操作列表
        /// </summary>
        public List<BatchOperation> GetOperations()
        {
            return _operations;
        }
    }

    public class SqliteDatabaseBatch : SqliteBatch
    {
        private readonly SqliteDatabase _database;

        internal SqliteDatabaseBatch(SqliteDatabase database)
        {
            _database = database;
        }

        public override UniTask<BatchResults> Commit(
            bool? exclusive = null,
            bool? noResult = null,
            bool? continueOnError = null
        )
        {
            _database.CheckNotClosed();

            return _database.Transaction<BatchResults>(txn =>
            {
                var sqliteTransaction = (SqliteTransaction)txn;
                return _database.TxnApplyBatch(
                    sqliteTransaction,
                    this,
                    noResult: noResult,
                    continueOnError: continueOnError
                );
            }, exclusive: exclusive);
        }

        public UniTask<BatchResults> Apply(bool? noResult = null, bool? continueOnError = null)
        {
            return _database.TxnApplyBatch(
                null,
                this,
                noResult: noResult,
                continueOnError: continueOnError
            );
        }
    }


    public class SqliteTransactionBatch : SqliteBatch
    {
        /// <summary>
        /// Our transaction (final in Dart, so private set in C#).
        /// </summary>
        private SqliteTransaction Transaction { get; }

        public SqliteTransactionBatch(SqliteTransaction transaction)
        {
            Transaction = transaction;
        }

        public override UniTask<BatchResults> Commit(
            bool? exclusive = null,
            bool? noResult = null,
            bool? continueOnError = null
        )
        {
            if (exclusive != null)
            {
                throw new ArgumentException(
                    "must not be set when commiting a batch in a transaction",
                    nameof(exclusive)
                );
            }

            return Apply(noResult, continueOnError);
        }

        public UniTask<BatchResults> Apply(bool? noResult = null, bool? continueOnError = null)
        {
            return Transaction.Database.TxnApplyBatch(
                Transaction,
                this,
                noResult,
                continueOnError
            );
        }
    }
}