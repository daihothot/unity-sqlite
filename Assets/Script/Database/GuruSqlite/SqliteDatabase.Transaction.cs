#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Guru.SDK.Framework.Utils.Extensions;
using Guru.SDK.Framework.Utils.Log;
using Newtonsoft.Json;
using Action = System.Action;

namespace GuruSqlite
{
    public partial class SqliteDatabase
    {
        // Set when parsing BEGIN and COMMIT/ROLLBACK
        protected bool inTransaction = false;

        // Set internally for testing
        protected bool doNotUseSynchronized = false;

        private static readonly object _rawLock = new object();


        // Lock warning configuration – can be set externally.
        public static TimeSpan? LockWarningDuration { get; set; } = null;
        public static Action? LockWarningCallback { get; set; } = null;


        public UniTask<T> Transaction<T>(Func<SqliteTransaction, UniTask<T>> action,
            bool? exclusive = null
        )
        {
            CheckNotClosed();
            return TxnWriteSynchronized<T>(_openTransaction,
                async (SqliteTransaction? txn) => await TxnTransaction(txn, action, exclusive));
        }

        public async UniTask<T> TxnTransaction<T>(
            SqliteTransaction? txn, System.Func<SqliteTransaction, UniTask<T>> action,
            bool? exclusive = null
        )
        {
            bool? successful = null;
            var transactionStarted = (txn == null);

            if (transactionStarted)
            {
                txn = await BeginTransaction(exclusive);
            }

            T result;
            try
            {
                // We assume 'action' expects a non-null Transaction,
                // so we pass 'txn!' or do a null check.
                result = await action(txn!);
                successful = true;
            }
            catch (SqliteTransactionRollbackSuccess<T> e)
            {
                // Capture the rollback result.
                result = e.Result;
            }
            finally
            {
                if (transactionStarted)
                {
                    var sqliteTransaction = txn!;
                    sqliteTransaction.Successful = successful;
                    await EndTransaction(sqliteTransaction);
                }
            }

            return result;
        }

        public async UniTask TxnBeginTransaction(
            SqliteTransaction txn,
            bool? exclusive = null
        )
        {
            object? response = null;

            // never create transaction in read-only mode
            if (!ReadOnly)
            {
                if (exclusive == true)
                {
                    response = await TxnExecute<object>(
                        txn,
                        "BEGIN EXCLUSIVE",
                        null,
                        beginTransaction: true
                    );
                }
                else
                {
                    response = await TxnExecute<object>(
                        txn,
                        "BEGIN IMMEDIATE",
                        null,
                        beginTransaction: true
                    );
                }
            }

            // Transaction v2 support, save the transaction id
            if (response is Dictionary<string, object?> map &&
                map.TryGetValue(GuruSqliteConstants.ParamTransactionId, out var transactionIdObj) &&
                transactionIdObj is int transactionIdVal)
            {
                txn.TransactionId = transactionIdVal;
            }
        }

        public UniTask<T> TxnSynchronized<T>(SqliteTransaction? txn, System.Func<SqliteTransaction?, UniTask<T>> action)
        {
            if (txn != null || doNotUseSynchronized)
            {
                txn?.CheckNotClosed();
                try
                {
                    return action(txn);
                }
                catch (SqliteDatabaseException e)
                {
                    if (e.TransactionClosed)
                    {
                        txn?.MarkClosed();
                    }

                    throw;
                }
            }
            else
            {
                var handleTimeoutWarning = (LockWarningDuration != null && LockWarningCallback != null);
                UniTaskCompletionSource<object?> timeoutCompleter = null;
                if (handleTimeoutWarning)
                {
                    timeoutCompleter = new UniTaskCompletionSource<object?>();
                }

                var operation = Synchronized(() =>
                {
                    if (handleTimeoutWarning)
                    {
                        timeoutCompleter?.TrySetResult(null);
                    }

                    return action(txn);
                });
                if (!handleTimeoutWarning || timeoutCompleter == null) return operation;
                if (LockWarningDuration != null)
                {
                    UniTask.Delay(LockWarningDuration.Value).ContinueWith(() =>
                    {
                        LockWarningCallback?.Invoke();
                        timeoutCompleter.TrySetResult(null);
                    }).Forget();
                }

                return operation;
            }
        }

        public UniTask<T>
            TxnWriteSynchronized<T>(SqliteTransaction? txn, System.Func<SqliteTransaction?, UniTask<T>> action) =>
            TxnSynchronized(txn, action);

        public UniTask<T> TxnExecute<T>(
            SqliteTransaction? txn,
            string sql,
            object?[]? arguments,
            bool? beginTransaction = null)
        {
            return TxnWriteSynchronized<T>(txn, async _ =>
            {
                var inTransactionChange = SqliteUtils.GetSqlInTransactionArgument(sql);
                if (inTransactionChange ?? false)
                {
                    inTransactionChange = true;
                    inTransaction = true;
                }
                else if (inTransactionChange == false)
                {
                    inTransactionChange = false;
                    inTransaction = false;
                }

                return await InvokeExecute<T>(
                    txn,
                    sql,
                    arguments,
                    inTransactionChange: inTransactionChange,
                    beginTransaction: beginTransaction);
            });
        }

        private UniTask<T> InvokeExecute<T>(
            SqliteTransaction? txn,
            string sql,
            object?[]? arguments,
            bool? inTransactionChange = null,
            bool? beginTransaction = null)
        {
            var methodArguments = TxnGetSqlMethodArguments(txn, sql, arguments);
            if (beginTransaction == true)
            {
                methodArguments[GuruSqliteConstants.ParamTransactionId] = null;
            }

            AddInTransactionChangeParam(methodArguments, inTransactionChange);
            return SafeInvokeMethod<T>(GuruSqliteConstants.MethodExecute, methodArguments);
        }

        public UniTask<long> TxnRawInsert(
            SqliteTransaction? txn,
            string sql,
            object?[]? arguments)
        {
            return TxnWriteSynchronized<long>(txn, async _ =>
            {
                var result = await SafeInvokeMethod<long?>(GuruSqliteConstants.MethodInsert,
                    TxnGetSqlMethodArguments(txn, sql, arguments));
                return result ?? 0;
            });
        }

        public UniTask<QueryResult> TxnRawQuery(
            SqliteTransaction? txn,
            string sql,
            object?[]? arguments)
        {
            return TxnSynchronized<QueryResult>(txn, async _ =>
            {
                var result = await SafeInvokeMethod<object?>(GuruSqliteConstants.MethodQuery,
                    TxnGetSqlMethodArguments(txn, sql, arguments));
                return result != null ? QueryResult.From(result) : QueryResult.Empty;
            });
        }
        
        public UniTask<int> TxnRawUpdate(
            SqliteTransaction? txn,
            string sql,
            object[]? arguments)
        {
            return TxnRawUpdateOrDelete(txn, sql, arguments);
        }
        
        public UniTask<int> TxnRawDelete(
            SqliteTransaction? txn,
            string sql,
            object[]? arguments)
        {
            return TxnRawUpdateOrDelete(txn, sql, arguments);
        }
        
        private UniTask<int> TxnRawUpdateOrDelete(
            SqliteTransaction? txn,
            string sql,
            object?[]? arguments
        )
        {
            return TxnWriteSynchronized<int>(txn, async _ =>
            {
                var methodArguments = BuildBaseDatabaseMethodArguments(txn, new Dictionary<string, object?>
                {
                    { GuruSqliteConstants.ParamSql, sql },
                    { GuruSqliteConstants.ParamSqlArguments, arguments }
                });
                Log.D($"TxnRawUpdateOrDelete: {sql} {JsonConvert.SerializeObject(arguments)}", "GuruSqlite");
                var result = await SafeInvokeMethod<int?>(GuruSqliteConstants.MethodUpdate, methodArguments);
                return result ?? 0;
            });
        }

        // Private helper for async lock acquisition.
        private UniTask<T> Synchronized<T>(System.Func<UniTask<T>> func)
        {
            lock (_rawLock)
            {
                return func.Invoke();
            }
        }

        // Determine if the SQL command marks a change in transaction state.


        // Build the argument dictionary for SQL method invocation.
        protected Dictionary<string, object?> TxnGetSqlMethodArguments(
            SqliteTransaction? txn,
            string sql,
            object?[]? arguments)
        {
            return BuildBaseDatabaseMethodArguments(txn, new Dictionary<string, object?>
            {
                [GuruSqliteConstants.ParamSql] = sql,
                [GuruSqliteConstants.ParamSqlArguments] = arguments
            });
        }

        // Add the inTransactionChange parameter if applicable.
        protected void AddInTransactionChangeParam(
            Dictionary<string, object?> methodArguments,
            bool? inTransactionChange)
        {
            if (inTransactionChange.HasValue)
            {
                methodArguments["inTransactionChange"] = inTransactionChange.Value;
            }
        }

        /// <summary>
        /// Base database map parameter.
        /// </summary>
        private Dictionary<string, object?> BuildBaseDatabaseMethodArguments(SqliteTransaction? txn,
            Dictionary<string, object?> methodArguments)
        {
            methodArguments[GuruSqliteConstants.ParamId] = Id;

            // transaction v2
            if (txn?.TransactionId != null)
            {
                methodArguments[GuruSqliteConstants.ParamTransactionId] = txn.TransactionId;
            }

            return methodArguments;
        }

        /// <summary>
        /// v1 and v2 support
        /// Base database map parameter in transaction.
        /// </summary>
        public Dictionary<string, object?> GetBaseDatabaseMethodArgumentsInTransactionChange(
            SqliteTransaction txn,
            bool? inTransaction)
        {
            var map = BuildBaseDatabaseMethodArguments(txn, new Dictionary<string, object?>());
            AddInTransactionChangeParam(map, inTransaction);
            return map;
        }

        // Invoke the SQL command safely.
        private UniTask<T> SafeInvokeMethod<T>(string method, Dictionary<string, object?> methodArguments)
        {
            return _factory.InvokeMethod<T>(method, methodArguments);
        }
        
        public UniTask<BatchResults> TxnApplyBatch(
            SqliteTransaction? txn,
            SqliteBatch batch,
            bool? noResult = null,
            bool? continueOnError = null
        )
        {
            return TxnWriteSynchronized(txn, async _ =>
            {
                var arguments = BuildBaseDatabaseMethodArguments(txn, new Dictionary<string, object?>
                {
                    { GuruSqliteConstants.ParamOperations , batch.GetOperationsParam() }
                });
                if (noResult == true)
                {
                    arguments[GuruSqliteConstants.ParamNoResult] = noResult;
                }
                if (continueOnError == true)
                {
                    arguments[GuruSqliteConstants.ParamContinueOnError] = continueOnError;
                }

                var results = await SafeInvokeMethod<List<object?>?>(GuruSqliteConstants.MethodBatch, arguments);


                return BatchResults.From(results ?? new List<object?>());
            });
        }

        public async UniTask<int> TxnGetVersion(SqliteTransaction? txn)
        {
            var rows = await TxnRawQuery(txn, "PRAGMA user_version", null);
            return SqliteUtils.FirstIntValue(rows) ?? 0;
        }

        public async UniTask TxnSetVersion(SqliteTransaction? txn, int version)
        {
            await TxnExecute<object>(txn, $"PRAGMA user_version = {version}", null);
        }
    }
}