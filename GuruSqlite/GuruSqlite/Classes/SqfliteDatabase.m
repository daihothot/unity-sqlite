#import "SqfliteDatabase.h"
#import "SqflitePlugin.h"
#import "SqfliteDarwinImport.h"
#import "GuruSqliteLog.h"

#import <sqlite3.h>

// iOS workaround bug #214
static NSString *const SqfliteSqlPragmaSqliteDefensiveOff = @"PRAGMA sqflite -- db_config_defensive_off";

static NSString *const _paramCursorPageSize = @"cursorPageSize";
static NSString *const _paramCursorId = @"cursorId";
static NSString *const _paramCancel = @"cancel";
// For batch
static NSString *const _paramOperations = @"operations";

static int transactionIdForce = -1;

// Import hidden method
@interface SqfliteDarwinDatabase ()
- (void)resultSetDidClose:(SqfliteDarwinResultSet *)resultSet;
@end

@implementation SqfliteDatabase

@synthesize databaseId, fmDatabaseQueue, cursorMap, logLevel, currentTransactionId, noTransactionOperationQueue, lastCursorId,lastTransactionId;


- (instancetype)init {
    self = [super init];
    if (self) {
        cursorMap = [NSMutableDictionary new];
        lastCursorId = 0;
        lastTransactionId = 0;
        noTransactionOperationQueue = [NSMutableArray new];
        LogDebug(@"SqfliteDatabase initialized with ID: %@", databaseId);
    }
    return self;
}


- (void)inDatabase:(void (^)(SqfliteDarwinDatabase *db))block {
    dispatch_async(dispatch_get_global_queue(DISPATCH_QUEUE_PRIORITY_DEFAULT, 0), ^{
        [self.fmDatabaseQueue inDatabase:block];
    });
}

- (void)dbHandleError:(SqfliteDarwinDatabase*)db result:(FlutterResult)result {
    // handle error
    NSString* errorMessage = [NSString stringWithFormat:@"%@", [db lastError]];
    LogError(@"Database error: %@", errorMessage);
    result([FlutterError errorWithCode:SqliteErrorCode
                               message:errorMessage
                               details:nil]);
}

- (void)dbHandleError:(SqfliteDarwinDatabase*)db operation:(SqfliteOperation*)operation {
    NSMutableDictionary* details = nil;
    NSString* sql = [operation getSql];
    if (sql != nil) {
        details = [NSMutableDictionary new];
        [details setObject:sql forKey:SqfliteParamSql];
        NSArray* sqlArguments = [operation getSqlArguments];
        if (sqlArguments != nil) {
            [details setObject:sqlArguments forKey:SqfliteParamSqlArguments];
        }
    }
    
    NSString* errorMessage = [NSString stringWithFormat:@"%@", [db lastError]];
    LogError(@"Database operation error: %@", errorMessage);
    if (sql != nil) {
        LogDebug(@"Failed SQL: %@", sql);
    }
    
    [operation error:([FlutterError errorWithCode:SqliteErrorCode
                                          message:errorMessage
                                          details:details])];
}

- (void)dbRunQueuedOperations:(SqfliteDarwinDatabase*)db {
    while (![SqflitePlugin arrayIsEmpty:noTransactionOperationQueue]) {
        if (currentTransactionId != nil) {
            break;
        }
        SqfliteQueuedOperation* queuedOperation = [noTransactionOperationQueue objectAtIndex:0];
        [noTransactionOperationQueue removeObjectAtIndex:0];
        LogDebug(@"Running queued operation from queue (remaining: %lu)", (unsigned long)[noTransactionOperationQueue count]);
        queuedOperation.handler(db, queuedOperation.operation);
    }
}

- (void)wrapSqlOperationHandler:(SqfliteDarwinDatabase*)db operation:(SqfliteOperation*)operation handler:(SqfliteOperationHandler)handler {
    NSNumber* transactionId = [operation getTransactionId];
    if (currentTransactionId == nil) {
        // ignore
        handler(db, operation);
    } else if (transactionId != nil && (transactionId.intValue == currentTransactionId.intValue || transactionId.intValue == transactionIdForce)) {
        LogDebug(@"Executing operation in transaction ID: %@", currentTransactionId);
        handler(db, operation);
        if (currentTransactionId == nil && ![SqflitePlugin arrayIsEmpty:noTransactionOperationQueue]) {
            [self dbRunQueuedOperations:db];
        }
    } else {
        // Queue for later
        SqfliteQueuedOperation* queuedOperation = [SqfliteQueuedOperation new];
        queuedOperation.operation = operation;
        queuedOperation.handler = handler;
        LogDebug(@"Operation queued for later execution (current transaction: %@)", currentTransactionId);
        [noTransactionOperationQueue addObject:queuedOperation];
    }
}
- (bool)dbDoExecute:(SqfliteDarwinDatabase*)db operation:(SqfliteOperation*)operation {
    if (![self dbExecuteOrError:db operation:operation]) {
        return false;
    }
    [operation success:[NSNull null]];
    return true;
}

- (void)dbExecute:(SqfliteDarwinDatabase*)db operation:(SqfliteOperation*)operation {
    [self wrapSqlOperationHandler:db operation:operation handler:^(SqfliteDarwinDatabase* db, SqfliteOperation* operation) {
        NSNumber* inTransactionChange = [operation getInTransactionChange];
        bool hasNullTransactionId  = [operation hasNullTransactionId];
        bool enteringTransaction = [inTransactionChange boolValue] == true && hasNullTransactionId;
        
        if (enteringTransaction) {
            self.currentTransactionId = [NSNumber numberWithInt:++self.lastTransactionId];
            LogInfo(@"Entering transaction ID: %@", self.currentTransactionId);
        }
        if ([self dbExecuteOrError:db operation:operation]) {
            if (enteringTransaction) {
                NSMutableDictionary* result = [NSMutableDictionary new];
                [result setObject:self.currentTransactionId       forKey:SqfliteParamTransactionId];
                [operation success:result];
            } else {
                bool leavingTransaction = inTransactionChange != nil && [inTransactionChange boolValue] == false;
                if (leavingTransaction) {
                    LogInfo(@"Leaving transaction ID: %@", self.currentTransactionId);
                    self.currentTransactionId = nil;
                }
                [operation success:[NSNull null]];
            }
        } else {
            if (enteringTransaction) {
                // On error revert change
                LogWarning(@"Transaction failed, reverting transaction ID: %@", self.currentTransactionId);
                self.currentTransactionId = nil;
            }
        }
    }];
}

- (bool)dbExecuteOrError:(SqfliteDarwinDatabase*)db operation:(SqfliteOperation*)operation {
    NSString* sql = [operation getSql];
    NSArray* sqlArguments = [operation getSqlArguments];
    NSNumber* inTransaction = [operation getInTransactionChange];
    
    // Handle Hardcoded workarounds
    // Handle issue #525
    if ([SqfliteSqlPragmaSqliteDefensiveOff isEqualToString:sql]) {
        LogDebug(@"Setting SQLITE_DBCONFIG_DEFENSIVE to OFF");
        sqlite3_db_config(db.sqliteHandle, SQLITE_DBCONFIG_DEFENSIVE, 0, 0);
    }
    
    BOOL argumentsEmpty = [SqflitePlugin arrayIsEmpty:sqlArguments];
    if (sqfliteHasSqlLogLevel(logLevel)) {
        NSLog(@"%@ %@", sql, argumentsEmpty ? @"" : sqlArguments);
    }
    
    BOOL success;
    if (!argumentsEmpty) {
        success = [db executeUpdate: sql withArgumentsInArray: sqlArguments];
    } else {
        success = [db executeUpdate: sql];
    }
    
    // If wanted, we leave the transaction even if it fails
    if (inTransaction != nil) {
        if (![inTransaction boolValue]) {
            self.inTransaction = false;
        }
    }
    
    // handle error
    if (!success) {
        LogError(@"SQL execution failed");
        [self dbHandleError:db operation:operation];
        return false;
    }
    LogDebug(@"SQL execution succeeded");
    
    // We enter the transaction on success
    if (inTransaction != nil) {
        if ([inTransaction boolValue]) {
            self.inTransaction = true;
        }
    }
    
    return true;
}


//
// insert
//
- (void)dbInsert:(SqfliteDarwinDatabase*)db operation:(SqfliteOperation*)operation {
    [self wrapSqlOperationHandler:db operation:operation handler:^(SqfliteDarwinDatabase* db, SqfliteOperation* operation) {
        [self dbDoInsert:db operation:operation];
    }];
}
- (bool)dbDoInsert:(SqfliteDarwinDatabase*)db operation:(SqfliteOperation*)operation {
    if (![self dbExecuteOrError:db operation:operation]) {
        return false;
    }
    if ([operation getNoResult]) {
        [operation success:[NSNull null]];
        return true;
    }
    // handle ON CONFLICT IGNORE (issue #164) by checking the number of changes
    // before
    int changes = [db changes];
    if (changes == 0) {
        if (sqfliteHasSqlLogLevel(self.logLevel)) {
            NSLog(@"no changes");
        }
        LogDebug(@"Insert operation - no changes (possible ON CONFLICT IGNORE)");
        [operation success:[NSNull null]];
        return true;
    }
    sqlite_int64 insertedId = [db lastInsertRowId];
    if (sqfliteHasSqlLogLevel(self.logLevel)) {
        NSLog(@"inserted %@", @(insertedId));
    }
    LogDebug(@"Inserted row ID: %lld", insertedId);
    [operation success:(@(insertedId))];
    return true;
}

- (void)dbUpdate:(SqfliteDarwinDatabase*)db operation:(SqfliteOperation*)operation {
    [self wrapSqlOperationHandler:db operation:operation handler:^(SqfliteDarwinDatabase* db, SqfliteOperation* operation) {
        [self dbDoUpdate:db operation:operation];
    }];
}
- (bool)dbDoUpdate:(SqfliteDarwinDatabase*)db operation:(SqfliteOperation*)operation {
    if (![self dbExecuteOrError:db operation:operation]) {
        return false;
    }
    if ([operation getNoResult]) {
        [operation success:[NSNull null]];
        return true;
    }
    int changes = [db changes];
    if (sqfliteHasSqlLogLevel(self.logLevel)) {
        NSLog(@"changed %@", @(changes));
    }
    LogDebug(@"Updated rows: %d", changes);
    [operation success:(@(changes))];
    return true;
}

//
// query
//
- (void)dbQuery:(SqfliteDarwinDatabase*)db operation:(SqfliteOperation*)operation {
    [self wrapSqlOperationHandler:db operation:operation handler:^(SqfliteDarwinDatabase* db, SqfliteOperation* operation) {
        [self dbDoQuery:db operation:operation];
    }];
}

- (bool)dbDoQuery:(SqfliteDarwinDatabase*)db operation:(SqfliteOperation*)operation {
    NSString* sql = [operation getSql];
    NSArray* sqlArguments = [operation getSqlArguments];
    bool argumentsEmpty = [SqflitePlugin arrayIsEmpty:sqlArguments];
    // Non null means use a cursor
    NSNumber* cursorPageSize = [operation getArgument:_paramCursorPageSize];
    
    if (sqfliteHasSqlLogLevel(self.logLevel)) {
        NSLog(@"%@ %@", sql, argumentsEmpty ? @"" : sqlArguments);
    }
    
    SqfliteDarwinResultSet *resultSet;
    if (!argumentsEmpty) {
        resultSet = [db executeQuery:sql withArgumentsInArray:sqlArguments];
    } else {
        // rs = [db executeQuery:sql];
        // This crashes on MacOS if there is any ? in the query
        // Workaround using an empty array
        resultSet = [db executeQuery:sql withArgumentsInArray:@[]];
    }
    
    // handle error
    if ([db hadError]) {
        LogError(@"Query execution failed");
        [self dbHandleError:db operation:operation];
        return false;
    }
    LogDebug(@"Query executed successfully");
    
    NSMutableDictionary* results = [SqflitePlugin resultSetToResults:resultSet cursorPageSize:cursorPageSize];
    
    if (cursorPageSize != nil) {
        bool cursorHasMoreData = [resultSet hasAnotherRow];
        if (cursorHasMoreData) {
            NSNumber* cursorId = [NSNumber numberWithInt:++self.lastCursorId];
            SqfliteCursor* cursor = [SqfliteCursor new];
            cursor.cursorId = cursorId;
            cursor.pageSize = cursorPageSize;
            cursor.resultSet = resultSet;
            self.cursorMap[cursorId] = cursor;
            LogDebug(@"Created cursor ID: %@ with page size: %@", cursorId, cursorPageSize);
            // Notify cursor support in the result
            results[_paramCursorId] = cursorId;
            // Prevent SqfliteDarwinDB warning, we keep a result set open on purpose
            [db resultSetDidClose:resultSet];
        }
    }
    [operation success:results];
    return true;
}




//
// query
//

- (void)dbQueryCursorNext:(SqfliteDarwinDatabase*)db operation:(SqfliteOperation*)operation {
    
    NSNumber* cursorId = [operation getArgument:_paramCursorId];
    NSNumber* cancelValue = [operation getArgument:_paramCancel];
    bool cancel = [cancelValue boolValue] == true;
    if (sqfliteHasVerboseLogLevel(self.logLevel))
    {
        NSLog(@"queryCursorNext %@%s", cursorId, cancel ? " (cancel)" : "");
    }
    LogDebug(@"Query cursor next for cursor ID: %@%@", cursorId, cancel ? @" (cancel requested)" : @"");
    
    if (cancel) {
        [self closeCursorById:cursorId];
        [operation success:[NSNull null]];
        return;
    } else {
        SqfliteCursor* cursor = self.cursorMap[cursorId];
        if (cursor == nil) {
            NSLog(@"cursor %@ not found.", cursorId);
            LogError(@"Cursor not found with ID: %@", cursorId);
            [operation success:[FlutterError errorWithCode:SqliteErrorCode
                                                   message: @"Cursor not found"
                                                   details:nil]];
            return;
        }
        SqfliteDarwinResultSet* resultSet = cursor.resultSet;
        NSMutableDictionary* results = [SqflitePlugin resultSetToResults:resultSet cursorPageSize:cursor.pageSize];
        
        bool cursorHasMoreData = [resultSet hasAnotherRow];
        if (cursorHasMoreData) {
            // Keep the cursorId to specify that we have more data.
            results[_paramCursorId] = cursorId;
            // Prevent SqfliteDarwinDB warning, we keep a result set open on purpose
            [db resultSetDidClose:resultSet];
            LogDebug(@"Cursor has more data - cursor ID: %@", cursorId);
        } else {
            LogDebug(@"Cursor reached end - closing cursor ID: %@", cursorId);
            [self closeCursor:cursor];
        }
        [operation success:results];
        
        
    }
}


- (void)dbBatch:(SqfliteDarwinDatabase*)db operation:(SqfliteMethodCallOperation*)mainOperation {
    
    bool noResult = [mainOperation getNoResult];
    bool continueOnError = [mainOperation getContinueOnError];
    
    NSArray* operations = [mainOperation getArgument:_paramOperations];
    NSMutableArray* operationResults = [NSMutableArray new];
    LogDebug(@"Starting batch execution with %lu operations", (unsigned long)[operations count]);
    for (NSDictionary* dictionary in operations) {
        // do something with object
        
        SqfliteBatchOperation* operation = [SqfliteBatchOperation new];
        operation.dictionary = dictionary;
        operation.noResult = noResult;
        
        NSString* method = [operation getMethod];
        if ([SqfliteMethodInsert isEqualToString:method]) {
            if ([self dbDoInsert:db operation:operation]) {
                [operation handleSuccess:operationResults];
            } else if (continueOnError) {
                [operation handleErrorContinue:operationResults];
            } else {
                [operation handleError:mainOperation.flutterResult];
                return;
            }
        } else if ([SqfliteMethodUpdate isEqualToString:method]) {
            if ([self dbDoUpdate:db operation:operation]) {
                [operation handleSuccess:operationResults];
            } else if (continueOnError) {
                [operation handleErrorContinue:operationResults];
            } else {
                [operation handleError:mainOperation.flutterResult];
                return;
            }
        } else if ([SqfliteMethodExecute isEqualToString:method]) {
            if ([self dbDoExecute:db operation:operation]) {
                [operation handleSuccess:operationResults];
            } else if (continueOnError) {
                [operation handleErrorContinue:operationResults];
            } else {
                [operation handleError:mainOperation.flutterResult];
                return;
            }
        } else if ([SqfliteMethodQuery isEqualToString:method]) {
            if ([self dbDoQuery:db operation:operation]) {
                [operation handleSuccess:operationResults];
            } else if (continueOnError) {
                [operation handleErrorContinue:operationResults];
            } else {
                [operation handleError:mainOperation.flutterResult];
                return;
            }
        } else {
            LogError(@"Unsupported batch method: %@", method);
            [mainOperation success:[FlutterError errorWithCode:SqfliteErrorBadParam
                                                       message:[NSString stringWithFormat:@"Batch method '%@' not supported", method]
                                                       details:nil]];
            return;
        }
    }
    
    LogDebug(@"Batch execution completed with %lu results", (unsigned long)[operationResults count]);
    if (noResult) {
        [mainOperation success:[NSNull null]];
    } else {
        [mainOperation success:operationResults];
    }
    
}
- (void)closeCursorById:(NSNumber*)cursorId {
    SqfliteCursor* cursor = cursorMap[cursorId];
    if (cursor != nil) {
        [self closeCursor:cursor];
    }
}

- (void)closeCursor:(SqfliteCursor*)cursor {
    NSNumber* cursorId = cursor.cursorId;
    if (sqfliteHasVerboseLogLevel(logLevel)) {
        NSLog(@"closing cursor %@", cursorId);
    }
    LogDebug(@"Closing cursor ID: %@", cursorId);
    [cursorMap removeObjectForKey:cursorId];
    [cursor.resultSet close];
}

@end
