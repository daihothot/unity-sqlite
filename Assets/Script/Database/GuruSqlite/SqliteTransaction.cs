#nullable enable
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GuruSqlite;

namespace GuruSqlite
{
    /// <summary>
    /// SQLite事务实现
    /// </summary>
    public class SqliteTransaction : SqliteExecutor
    {
        
        /// Optional transaction id, depending on the implementation
        internal int? TransactionId;

        /// True if the transaction has already been terminated (rollback or commit)
        internal bool? Closed;
        

        /// <summary>
        /// The transaction database.
        /// </summary>
        public SqliteDatabase Db { get; }


        public override SqliteDatabase Database => Db;
        
        protected override SqliteTransaction? Txn => this;
        
        /// <summary>
        /// Create a transaction on a given database.
        /// </summary>
        public SqliteTransaction(SqliteDatabase database)
        {
            Db = database;
        }
        
        internal override void CheckNotClosed()
        {
            if (Closed == true)
            {
                throw new SqliteDatabaseException("error transaction_closed", null, null);
            }
        }

        /// <summary>
        /// Mark a transaction as closed.
        /// </summary>
        public void MarkClosed()
        {
            Closed = true;
        }

        /// <summary>
        /// True if a transaction is successful.
        /// </summary>
        public bool? Successful { get; set; }

        /// <summary>
        /// Returns this transaction itself.
        /// </summary>


        /// <summary>
        /// Create a batch for this transaction.
        /// </summary>
        public override SqliteBatch Batch()
        {
            return new SqliteTransactionBatch(this);
        }
    }
}