#nullable enable
using Cysharp.Threading.Tasks;

namespace GuruSqlite
{
    public static class DatabaseExecutorExtension
    {
        private static SqliteDatabase GetDatabase(this IDatabaseExecutor executor)
        {
            return executor switch
            {
                SqliteDatabase db => db,
                SqliteTransaction txn => txn.Db,
                _ => throw new System.InvalidOperationException("Invalid executor type")
            };
        }
        private static SqliteTransaction? GetTransaction(this IDatabaseExecutor executor)
        {
            return executor as SqliteTransaction;
        }

        public static UniTask SetVersion(this IDatabaseExecutor executor, int version)
        {
            var db = GetDatabase(executor);
            db.CheckNotClosed();
            return db.TxnSetVersion(GetTransaction(executor), version);
        }

        public static UniTask<int> GetVersion(this IDatabaseExecutor executor)
        {
            var db = GetDatabase(executor);
            db.CheckNotClosed();
            return db.TxnGetVersion(GetTransaction(executor));
        }
    }
}