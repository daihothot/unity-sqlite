namespace GuruSqlite
{
using System;

/// <summary>
/// Constants for GuruSQLite operations
/// </summary>
public static class GuruSqliteConstants
{
    //
    // Native methods to use
    //

    /// <summary>Native sql INSERT.</summary>
    public const string MethodInsert = "insert";

    /// <summary>Native batch.</summary>
    public const string MethodBatch = "batch";

    /// <summary>Native debug method.</summary>
    public const string MethodDebug = "debug";

    /// <summary>Native options method.</summary>
    public const string MethodOptions = "options";

    /// <summary>Native close database method.</summary>
    public const string MethodCloseDatabase = "closeDatabase";

    /// <summary>Native open database method.</summary>
    public const string MethodOpenDatabase = "openDatabase";

    /// <summary>Native sql execute.</summary>
    public const string MethodExecute = "execute";

    /// <summary>Native sql UPDATE or DELETE method.</summary>
    public const string MethodUpdate = "update";

    /// <summary>Native sql SELECT method.</summary>
    public const string MethodQuery = "query";

    /// <summary>Native sql SELECT method.</summary>
    public const string MethodQueryCursorNext = "queryCursorNext";

    /// <summary>deprecated.</summary>
    public const string MethodGetPlatformVersion = "getPlatformVersion";

    /// <summary>Native getDatabasePath method.</summary>
    public const string MethodGetDatabasesPath = "getDatabasesPath";

    /// <summary>Native database exists method.</summary>
    public const string MethodDatabaseExists = "databaseExists";

    /// <summary>Native database delete method.</summary>
    public const string MethodDeleteDatabase = "deleteDatabase";

    /// <summary>Native write database bytes method.</summary>
    public const string MethodWriteDatabaseBytes = "writeDatabaseBytes";

    /// <summary>Native read database bytes method.</summary>
    public const string MethodReadDatabaseBytes = "readDatabaseBytes";

    /// <summary>Native batch operations parameter.</summary>
    public const string ParamOperations = "operations";

    /// <summary>
    /// Native batch 'no result' flag.
    /// if true the result of each batch operation is not filled
    /// </summary>
    public const string ParamNoResult = "noResult";

    /// <summary>
    /// Native batch 'continue on error' flag.
    /// if true all the operation in the batch are executed even if on failed.
    /// </summary>
    public const string ParamContinueOnError = "continueOnError";

    /// <summary>Batch operation method (insert/execute/query/update</summary>
    public const string ParamMethod = "method";

    /// <summary>Batch operation result.</summary>
    public const string ParamResult = "result";

    /// <summary>Error.</summary>
    public const string ParamError = "error";

    /// <summary>Error code.</summary>
    public const string ParamErrorCode = "code";

    /// <summary>Error message.</summary>
    public const string ParamErrorMessage = "message";

    /// <summary>Error message.</summary>
    public const string ParamErrorResultCode = "resultCode";

    /// <summary>Error data.</summary>
    public const string ParamErrorData = "data";

    /// <summary>
    /// Open database 'recovered' flag.
    /// True if a single instance was recovered from the native world.
    /// </summary>
    public const string ParamRecovered = "recovered";

    /// <summary>
    /// Open database 'recovered in transaction' flag.
    /// True if a single instance was recovered from the native world
    /// while in a transaction.
    /// </summary>
    public const string ParamRecoveredInTransaction = "recoveredInTransaction";

    /// <summary>The database path (string).</summary>
    public const string ParamPath = "path";

    /// <summary>Bytes content.</summary>
    public const string ParamBytes = "bytes";

    /// <summary>The database version (int).</summary>
    public const string ParamVersion = "version";

    /// <summary>The database id (int)</summary>
    public const string ParamId = "id";

    /// <summary>True if the database is in a transaction</summary>
    public const string ParamInTransaction = "inTransaction";

    /// <summary>
    /// For beginTransaction, set it to null
    /// Returned by beingTransaction for new implementation
    /// Transaction param, to set in all calls during a transaction.
    /// To set to null when beginning a transaction, it tells the implementation
    /// that transactionId is supported by the client (compared to a raw BEGIN calls)
    /// </summary>
    public const string ParamTransactionId = "transactionId";

    /// <summary>Special transaction id to force even if a transaction is running.</summary>
    public const int ParamTransactionIdValueForce = -1;

    /// <summary>True when opening the database (bool)</summary>
    public const string ParamReadOnly = "readOnly";

    /// <summary>True if opened as a single instance (bool)</summary>
    public const string ParamSingleInstance = "singleInstance";

    /// <summary>
    /// SQL query (insert/execute/update/select).
    /// String.
    /// </summary>
    public const string ParamSql = "sql";

    /// <summary>
    /// SQL query parameters.
    /// List.
    /// </summary>
    public const string ParamSqlArguments = "arguments";

    /// <summary>
    /// SQL query cursorId parameter.
    /// Integer.
    /// </summary>
    public const string ParamCursorId = "cursorId";

    /// <summary>
    /// SQL query cursor page size parameter.
    /// If null to cursor is used
    /// Integer.
    /// </summary>
    public const string ParamCursorPageSize = "cursorPageSize";
    
    public const string ErrorBadParam = "bad_param"; // internal only
    public const string ErrorOpenFailed = "open_failed"; 

    /// <summary>
    /// SQL query cursor next cancel parameter.
    /// true or false
    /// boolean.
    /// </summary>
    public const string ParamCursorCancel = "cancel";

    /// <summary>SQLite error code</summary>
    public const string SqliteErrorCode = "sqlite_error";

    /// <summary>Internal error code</summary>
    public const string InternalErrorCode = "internal";

    /// <summary>Special database name opened in memory</summary>
    public const string InMemoryDatabasePath = ":memory:";

    /// <summary>
    /// Default duration before printing a lock warning if a database call hangs.
    /// Non final for changing it during testing.
    /// If a database called is delayed by this duration, a print will happen.
    /// </summary>
    public static readonly TimeSpan LockWarningDurationDefault = TimeSpan.FromSeconds(10);

    //
    // Log levels
    //
    /// <summary>No logs</summary>
    public static readonly int SqfliteLogLevelNone = 0;

    /// <summary>Log native sql commands</summary>
    public static readonly int SqfliteLogLevelSql = 1;

    /// <summary>Log native verbose</summary>
    public static readonly int SqfliteLogLevelVerbose = 2;

    // deprecated since 1.1.6
    // @deprecated
    /// <summary>deprecated</summary>
    public const string MethodSetDebugModeOn = "debugMode";

    /// <summary>Default buffer size for queryCursor</summary>
    public const int QueryCursorBufferSizeDefault = 100;
}
}