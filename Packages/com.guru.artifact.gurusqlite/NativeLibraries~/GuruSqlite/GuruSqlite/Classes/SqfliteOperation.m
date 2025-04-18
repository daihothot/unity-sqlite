//
//  Operation.m
//  sqflite
//
//  Created by Alexandre Roux on 09/01/2018.
//

#import <Foundation/Foundation.h>
#import "SqfliteOperation.h"
#import "SqflitePlugin.h"
#import "SqfliteDarwinImport.h"
#import "GuruSqliteLog.h"

// Abstract
@implementation SqfliteOperation

- (NSString*)getMethod {
    return  nil;
}
- (NSString*)getSql {
    return nil;
}
- (NSArray*)getSqlArguments {
    return nil;
}

- (bool)getNoResult {
    return false;
}
- (bool)getContinueOnError {
    return false;
}
- (void)success:(NSObject*)results {
    LogDebug(@"Operation succeeded (base class - override not called)");
}

- (void)error:(NSObject*)error {
    LogError(@"Operation failed (base class - override not called): %@", error);
}

// To override
- (id)getArgument:(NSString*)key {
    return nil;
}

// To override
- (bool)hasArgument:(NSString*)key {
    return false;
}

// Either nil or NSNumber
- (NSNumber*)getTransactionId {
    // It might be NSNull (for begin transaction)
    id rawId = [self getArgument:SqfliteParamTransactionId];
    if ([rawId isKindOfClass:[NSNumber class]]) {
        LogDebug(@"Transaction ID found: %@", rawId);
        return rawId;
    }
    LogDebug(@"No valid transaction ID found");
    return nil;
}

- (NSNumber*)getInTransactionChange {
    return [self getArgument:SqfliteParamInTransactionChange];
}

- (bool)hasNullTransactionId {
    return [self getArgument:SqfliteParamTransactionId] == [NSNull null];
}

@end

@implementation SqfliteBatchOperation

@synthesize dictionary, results, error, noResult, continueOnError;

- (NSString*)getMethod {
    return [dictionary objectForKey:SqfliteParamMethod];
}

- (NSString*)getSql {
    return [dictionary objectForKey:SqfliteParamSql];
}

- (NSArray*)getSqlArguments {
    NSArray* arguments = [dictionary objectForKey:SqfliteParamSqlArguments];
    return [SqflitePlugin toSqlArguments:arguments];
}

- (bool)getNoResult {
    return noResult;
}

- (bool)getContinueOnError {
    return continueOnError;
}

- (void)success:(NSObject*)results {
    LogDebug(@"Batch operation succeeded");
    self.results = results;
}

- (void)error:(FlutterError*)error {
    LogError(@"Batch operation failed: %@ - %@", error.code, error.message);
    self.error = error;
}

- (void)handleSuccess:(NSMutableArray*)results {
    if (![self getNoResult]) {
        // We wrap the result in 'result' map
        [results addObject:[NSDictionary dictionaryWithObject:((self.results == nil) ? [NSNull null] : self.results)
                                                       forKey:SqfliteParamResult]];
    }
}

// Encore the flutter error in a map
- (void)handleErrorContinue:(NSMutableArray*)results {
    if (![self getNoResult]) {
        // We wrap the error in an 'error' map
        NSMutableDictionary* error = [NSMutableDictionary new];
        error[SqfliteParamErrorCode] = self.error.code;
        if (self.error.message != nil) {
            error[SqfliteParamErrorMessage] = self.error.message;
        }
        if (self.error.details != nil) {
            error[SqfliteParamErrorData] = self.error.details;
        }
        [results addObject:[NSDictionary dictionaryWithObject:error
                                                       forKey:SqfliteParamError]];
    }
}

- (void)handleError:(FlutterResult)result {
    result(error);
}

- (id)getArgument:(NSString*)key {
    return [dictionary objectForKey:key];
}

- (bool)hasArgument:(NSString*)key {
    return [self getArgument:key] != nil;
}

@end

@implementation SqfliteMethodCallOperation

@synthesize flutterMethodCall;
@synthesize flutterResult;

+ (SqfliteMethodCallOperation*)newWithCall:(FlutterMethodCall*)flutterMethodCall result:(FlutterResult)flutterResult {
    SqfliteMethodCallOperation* operation = [SqfliteMethodCallOperation new];
    operation.flutterMethodCall = flutterMethodCall;
    operation.flutterResult = flutterResult;
    return operation;
}

- (NSString*)getMethod {
    return flutterMethodCall.method;
}

- (NSString*)getSql {
    return flutterMethodCall.arguments[SqfliteParamSql];
}

- (bool)getNoResult {
    NSNumber* noResult = flutterMethodCall.arguments[SqfliteParamNoResult];
    return [noResult boolValue];
}

- (bool)getContinueOnError {
    NSNumber* noResult = flutterMethodCall.arguments[SqfliteParamContinueOnError];
    return [noResult boolValue];
}

- (NSArray*)getSqlArguments {
    NSArray* arguments = flutterMethodCall.arguments[SqfliteParamSqlArguments];
    return [SqflitePlugin toSqlArguments:arguments];
}



- (void)success:(NSObject*)results {
    LogDebug(@"Method call operation succeeded: %@", [self getMethod]);
    flutterResult(results);
}

- (void)error:(NSObject*)error {
    NSString* method = [self getMethod];
    NSString* sql = [self getSql];
    if ([error isKindOfClass:[FlutterError class]]) {
        FlutterError* flutterError = (FlutterError*)error;
        LogError(@"Method call operation failed: %@ - SQL: %@, Error: %@ - %@",
                method, sql ?: @"none", flutterError.code, flutterError.message);
    } else {
        LogError(@"Method call operation failed: %@ - SQL: %@, Error: %@",
                method, sql ?: @"none", error);
    }
    flutterResult(error);
}

- (id)getArgument:(NSString*)key {
    return flutterMethodCall.arguments[key];
}

@end

@implementation SqfliteQueuedOperation

@synthesize operation, handler;

@end
