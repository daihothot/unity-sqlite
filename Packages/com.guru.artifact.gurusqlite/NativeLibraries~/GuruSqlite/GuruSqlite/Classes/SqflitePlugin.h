//
//  SqflitePlugin.h
//  sqflite
//
//  Created by Alexandre Roux on 24/10/2022.
//
#ifndef SqflitePlugin_h
#define SqflitePlugin_h

#import "SqflitePluginPublic.h"

extern NSString *const SqfliteMethodExecute;;
extern NSString *const SqfliteMethodInsert;
extern NSString *const SqfliteMethodUpdate;
extern NSString *const SqfliteMethodQuery;

extern NSString *const SqfliteErrorBadParam;
extern NSString *const SqliteErrorCode;

extern NSString *const SqfliteParamMethod;
extern NSString *const SqfliteParamSql;
extern NSString *const SqfliteParamSqlArguments;
extern NSString *const SqfliteParamInTransactionChange;
extern NSString *const SqfliteParamNoResult;
extern NSString *const SqfliteParamContinueOnError;
extern NSString *const SqfliteParamResult;
extern NSString *const SqfliteParamError;
extern NSString *const SqfliteParamErrorCode;
extern NSString *const SqfliteParamErrorMessage;
extern NSString *const SqfliteParamErrorData;
extern NSString *const SqfliteParamTransactionId;

// Static helpers
static const int sqfliteLogLevelNone = 0;
static const int sqfliteLogLevelSql = 1;
static const int sqfliteLogLevelVerbose = 2;

extern bool sqfliteHasSqlLogLevel(int logLevel);
// True for verbose debugging
extern bool sqfliteHasVerboseLogLevel(int logLevel);

//// SqflitePlugin interface declaration
//@interface SqflitePlugin : NSObject
//
///**
// * Returns the shared instance of the plugin.
// * For Unity integration, this allows accessing the plugin instance from the bridge.
// */
//+ (instancetype)sharedInstance;
//
///**
// * Handles a method call from Flutter or Unity.
// */
//- (void)handleMethod:(FlutterMethodCall*)call result:(FlutterResult)result;
//
//@end

#endif // SqflitePlugin_h
