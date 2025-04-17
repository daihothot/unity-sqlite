#nullable enable
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Guru.SDK.Framework.Utils.Log;

namespace GuruSqlite
{
    public abstract class SqliteExecutor : IDatabaseExecutor
    {
        public abstract SqliteDatabase Database { get; }
        protected abstract SqliteTransaction? Txn { get; }

        internal abstract void CheckNotClosed();


        public UniTask Execute(string sql, object?[]? arguments = null)
        {
            Database.CheckNotClosed();
            return Database.TxnExecute<dynamic>(Txn, sql, arguments);
        }

        public UniTask<long> RawInsert(string sql, object?[]? arguments = null)
        {
            Database.CheckNotClosed();
            return Database.TxnRawInsert(Txn, sql, arguments);
        }

        public UniTask<long> Insert(string table, Dictionary<string, object?> values, string? nullColumnHack = null,
            ConflictAlgorithm? conflictAlgorithm = null)
        {
            var builder = SqlBuilder.Insert(
                table,
                values,
                nullColumnHack: nullColumnHack,
                conflictAlgorithm: conflictAlgorithm
            );
            return RawInsert(builder.Sql, builder.Arguments);
        }

        public UniTask<QueryResult> RawQuery(string sql, object?[]? arguments = null)
        {
            Database.CheckNotClosed();
            return Database.TxnRawQuery(Txn, sql, arguments);
        }

        public UniTask<QueryResult> Query(string table, bool? distinct = null,
            string[]? columns = null, string? where = null,
            object[]? whereArgs = null, string? groupBy = null, string? having = null, string? orderBy = null,
            int? limit = null, int? offset = null)
        {
            var builder = SqlBuilder.Query(
                table,
                distinct ?? false,
                columns,
                where,
                whereArgs,
                groupBy,
                having,
                orderBy,
                limit,
                offset
            );
            return RawQuery(builder.Sql, builder.Arguments);
        }

        public UniTask<int> RawUpdate(string sql, object[]? arguments = null)
        {
            Database.CheckNotClosed();
            return Database.TxnRawUpdate(Txn, sql, arguments);
        }

        public UniTask<int> Update(string table, Dictionary<string, object?> values, string? where = null,
            object[]? whereArgs = null,
            ConflictAlgorithm? conflictAlgorithm = null)
        {
            var builder = SqlBuilder.Update(
                table,
                values,
                where,
                whereArgs,
                conflictAlgorithm: conflictAlgorithm
            );
            
            Log.D($"Sql:{builder.Sql}  Arguments:{builder.Arguments}");
            
            return RawUpdate(builder.Sql, builder.Arguments);
        }

        public UniTask<int> RawDelete(string sql, object[]? arguments = null)
        {
            Database.CheckNotClosed();
            return Database.TxnRawDelete(Txn, sql, arguments);
        }

        public UniTask<int> Delete(string table, string? where = null, object[]? whereArgs = null)
        {
            var builder = SqlBuilder.Delete(
                table,
                where,
                whereArgs
            );
            return RawDelete(builder.Sql, builder.Arguments);
        }

        public abstract SqliteBatch Batch();

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}