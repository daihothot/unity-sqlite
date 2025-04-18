#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Guru.SDK.Framework.Utils.Extensions;
using Guru.SDK.Framework.Utils.Log;

namespace GuruSqlite
{
    /// <summary>
    /// SQLite数据库实现
    /// </summary>
    public partial class SqliteDatabase : SqliteExecutor
    {
        private readonly SqliteDatabaseFactory _factory;
        private readonly SqliteDatabaseOpenHelper _helper;
        private readonly string _path;
        private int? _id;
        private bool _isOpen;
        private SqliteTransaction? _openTransaction;

        public bool _isClosed = false;
        public OpenDatabaseOptions? Options { get; internal set; }

        public int? Id => _id;
        
        public bool ReadOnly => Options?.ReadOnly ?? false;


        /// <summary>
        /// Set when parsing BEGIN and COMMIT/ROLLBACK
        /// </summary>
        public bool InTransaction = false;

        /// <summary>
        /// Set internally for testing
        /// </summary>
        public bool DoNotUseSynchronized = false;

        /// <summary>
        /// 构造函数
        /// </summary>
        public SqliteDatabase(SqliteDatabaseFactory factory, SqliteDatabaseOpenHelper helper, string path)
        {
            _factory = factory;
            _helper = helper;
            _path = path;
        }

        /// <summary>
        /// 数据库是否打开
        /// </summary>
        public bool IsOpen => _isOpen;

        /// <summary>
        /// 数据库路径
        /// </summary>
        public string Path => _path;


        public override SqliteDatabase Database => this;
        protected override SqliteTransaction? Txn => _openTransaction;

        internal override void CheckNotClosed()
        {
            
        }

        /// <summary>
        /// Opens a database connection and returns its id
        /// </summary>
        /// <exception cref="DatabaseException"></exception>
        /// <returns>The database connection ID</returns>
        private async UniTask<int> OpenDatabaseConnection()
        {
            var parameters = new Dictionary<string, object> { { "path", Path } };

            if (Options is { ReadOnly: true })
            {
                parameters[GuruSqliteConstants.ParamReadOnly] = true;
            }

            // Single instance? never for standard in-memory database
            var singleInstance = (Options?.SingleInstance ?? false) && !Sqlite.IsInMemoryDatabasePath(Path);
            parameters[GuruSqliteConstants.ParamSingleInstance] = singleInstance;

            // Open the database connection
            var openResult = await _factory.InvokeMethod<object>(GuruSqliteConstants.MethodOpenDatabase, parameters);

            switch (openResult)
            {
                case int intResult:
                    return intResult;
                case Dictionary<string, object> resultMap:
                {
                    int? id = resultMap.TryGetValue(GuruSqliteConstants.ParamId, out var value) ? Convert.ToInt32(value)  : null;

                    // Recover means we found an instance in the native world
                    var recoveredInTransaction = resultMap.ContainsKey(GuruSqliteConstants.ParamRecoveredInTransaction) &&
                                                 (bool)resultMap[GuruSqliteConstants.ParamRecoveredInTransaction];

                    // In this case, we're going to rollback any changes in case a transaction
                    // was in progress. This catches hot-restart scenarios
                    if (recoveredInTransaction)
                    {
                        // Don't do it for read-only
                        if (Options?.ReadOnly == false)
                        {
                            // We are not yet open so invoke the native method directly
                            try
                            {
                                await _factory.InvokeMethod<object>(GuruSqliteConstants.MethodExecute, new Dictionary<string, object?>
                                {
                                    { GuruSqliteConstants.ParamSql, "ROLLBACK" },
                                    { GuruSqliteConstants.ParamId, id },

                                    // Force the action even if we are in a transaction
                                    { GuruSqliteConstants.ParamTransactionId, "force" },
                                    { GuruSqliteConstants.ParamInTransaction, false }
                                }.FilterOutNulls());
                            }
                            catch (Exception e)
                            {
                                Log.W($"Ignoring recovered database ROLLBACK error: {e}");
                            }
                        }
                    }

                    if (id.HasValue)
                    {
                        return id.Value;
                    }
                    else
                    {
                        throw new SqliteDatabaseException("No database ID returned", "open_failed");
                    }
                }
                default:
                    throw new SqliteDatabaseException($"Unsupported result type: {openResult?.GetType()}", "open_failed");
            }
        }

        /// <summary>
        /// 打开数据库
        /// </summary>
        /// <summary>
        /// Opens the database with the specified options
        /// </summary>
        /// <param name="options">The database options</param>
        /// <returns>This database instance</returns>
        internal async UniTask<SqliteDatabase> DoOpen(OpenDatabaseOptions options)
        {
            // Validate options
            if (options.Version.HasValue)
            {
                if (options.Version == 0)
                {
                    throw new ArgumentException("Version cannot be set to 0 in OpenDatabase");
                }
            }
            else
            {
                if (options.OnCreate != null)
                {
                    throw new ArgumentException("OnCreate must be null if no version is specified");
                }

                if (options.OnUpgrade != null)
                {
                    throw new ArgumentException("OnUpgrade must be null if no version is specified");
                }

                if (options.OnDowngrade != null)
                {
                    throw new ArgumentException("OnDowngrade must be null if no version is specified");
                }
            }

            Options = options;
            var databaseId = await OpenDatabaseConnection();

            try
            {
                // Special case for handling database downgrade by deleting and recreating
                options.OnDowngrade ??= async (database, oldVersion, newVersion) =>
                {
                    Log.I("Deleting database for downgrade");
                    // This is tricky as we're in the middle of opening a database
                    // Need to close what is being done and restart
                    await database.DoClose();

                    // But don't mark it as closed
                    _isClosed = false;

                    await _factory.DeleteDatabase(database.Path);

                    // Get a new database id after open
                    _id = databaseId = await OpenDatabaseConnection();

                    try
                    {
                        // Since we deleted the database, re-run the needed first steps:
                        // OnConfigure then OnCreate
                        if (options.OnConfigure != null)
                        {
                            await options.OnConfigure(database);
                        }
                    }
                    catch (Exception e)
                    {
                        // This exception is sometimes hard to catch during development
                        Log.W("Error during OnConfigure: " + e);

                        // Create a transaction just to make the current transaction happy
                        _openTransaction = await database.BeginTransaction(true);
                        throw;
                    }

                    // Recreate a new transaction
                    // No end transaction - it will be done later before calling OnOpen
                    _openTransaction = await database.BeginTransaction(true);
                    if (options.OnCreate != null)
                    {
                        await options.OnCreate(database, options.Version ?? -1);
                    }
                };

                _id = databaseId;

                Log.I("Opened database with id: " + _id, "GuruSqlite");
                // First configure it
                if (options.OnConfigure != null)
                {
                    await options.OnConfigure(this);
                }
                Log.I($"Opened database with Version: {options.Version}", "GuruSqlite");
                if (options.Version.HasValue)
                {
                    // Check the version outside of the transaction
                    // And only create the transaction if needed
                    var oldVersion = await this.GetVersion();
                    Log.I($"{oldVersion} => {options.Version}", "GuruSqlite");
                    if (oldVersion != options.Version)
                    {
                        try
                        {
                            await Transaction(async (txn) =>
                            {
                                // Set the current transaction as the open one
                                // to allow direct database calls during open and allowing
                                // creating a fake transaction (since we are already in a transaction)
                                var sqliteTransaction = (SqliteTransaction)txn;
                                _openTransaction = sqliteTransaction;

                                // Read again the version to be safe regarding edge cases
                                oldVersion = await TxnGetVersion(txn);
                                if (oldVersion == 0)
                                {
                                    if (options.OnCreate != null)
                                    {
                                        await options.OnCreate(this, options.Version.Value);
                                    }
                                    else if (options.OnUpgrade != null)
                                    {
                                        await options.OnUpgrade(this, 0, options.Version.Value);
                                    }
                                }
                                else if (options.Version.Value > oldVersion)
                                {
                                    if (options.OnUpgrade != null)
                                    {
                                        await options.OnUpgrade(this, oldVersion, options.Version.Value);
                                    }
                                }
                                else if (options.Version.Value < oldVersion)
                                {
                                    if (options.OnDowngrade != null)
                                    {
                                        await options.OnDowngrade(this, oldVersion, options.Version.Value);

                                        // Check and reuse transaction if needed
                                        // in case downgrade delete was called
                                        if (_openTransaction.TransactionId != sqliteTransaction.TransactionId)
                                        {
                                            sqliteTransaction.TransactionId = _openTransaction.TransactionId;
                                        }
                                    }
                                }

                                if (oldVersion != options.Version)
                                {
                                    await this.SetVersion(options.Version.Value);
                                }

                                return true; // Success
                            }, true); // Use exclusive transaction
                        }
                        finally
                        {
                            // Clean up open transaction
                            _openTransaction = null;
                        }
                    }
                }

                if (options.OnOpen != null)
                {
                    await options.OnOpen(this);
                }

                return this;
            }
            catch (Exception e)
            {
                Log.W($"Error {e} during open, closing...");
                await DoClose();
                throw;
            }
            finally
            {
                // Clean up open transaction
                _openTransaction = null;
            }
        }

        /// <summary>
        /// 关闭数据库
        /// </summary>
        internal async UniTask DoClose()
        {
            if (_id != null)
            {
                try
                {
                    await _factory.InvokeMethod<object>(GuruSqliteConstants.MethodCloseDatabase, new object[] { _id.Value });
                }
                catch (Exception ex)
                {
                    // 记录错误但不抛出
                    UnityEngine.Debug.LogError($"Error closing database: {ex.Message}");
                }
                finally
                {
                    _id = null;
                    _isOpen = false;
                }
            }
        }

        /// <summary>
        /// 关闭数据库
        /// </summary>
        public UniTask Close()
        {
            return _helper.CloseDatabase();
        }


        /// <summary>
        /// Create a new transaction.
        /// </summary>
        public SqliteTransaction NewTransaction()
        {
            var txn = new SqliteTransaction(this);
            return txn;
        }

        public UniTask<SqliteTransaction> BeginTransaction(bool? exclusive = null)
        {
            var txn = NewTransaction();
            return TxnBeginTransaction(txn, exclusive).ContinueWith(() => txn);
        }

        public UniTask EndTransaction(SqliteTransaction txn)
        {
            // never commit transaction in read-only mode
            if (ReadOnly) return UniTask.CompletedTask;
            if (txn.Closed == true)
            {
                inTransaction = false;
            }
            else
            {
                try
                {
                    return TxnExecute<dynamic>(txn, txn.Successful == true ? "COMMIT" : "ROLLBACK", null);
                }
                finally
                {
                    txn.Closed = true;
                }
            }
            return UniTask.CompletedTask;
        }


        /// <summary>
        /// 执行批处理操作
        /// </summary>
        public async UniTask<List<object?>> ApplyBatch(IBatch batch, bool noResult = false,
            bool continueOnError = false)
        {
            CheckNotClosed();

            var sqliteBatch = (SqliteBatch)batch;
            var operations = sqliteBatch.GetOperations();

            if (operations.Count == 0)
            {
                return new List<object?>();
            }

            var args = new List<object>
            {
                _id!.Value,
                operations,
                noResult,
                continueOnError
            };

            var results = await _factory.InvokeMethod<List<object?>>("batch", args.ToArray());
            return results;
        }

        /// <summary>
        /// 执行事务内的SQL命令
        /// </summary>
        internal async UniTask<T> ExecuteTransaction<T>(string method, object[] arguments,
            CancellationToken cancellationToken = default)
        {
            CheckNotClosed();

            var args = new List<object> { _id!.Value };
            args.AddRange(arguments);

            var result = await _factory.InvokeMethod<T>(method, args.ToArray());
            return result;
        }

        /// <summary>
        /// 处理SQL命令
        /// </summary>
        internal async UniTask<T> InvokeMethod<T>(string method, object[] arguments)
        {
            CheckNotClosed();

            var args = new List<object> { _id!.Value };
            args.AddRange(arguments);

            return await _factory.InvokeMethod<T>(method, args.ToArray());
        }


        public override SqliteBatch Batch()
        {
            return new SqliteDatabaseBatch(this);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Close().Forget();
        }
    }
}