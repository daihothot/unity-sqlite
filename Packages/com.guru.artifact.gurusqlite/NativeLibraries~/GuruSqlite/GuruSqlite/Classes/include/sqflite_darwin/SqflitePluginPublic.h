//
//  SqflitePlugin.h
//  sqflite
//
//  Created by Alexandre Roux on 24/10/2022.
//
#ifndef SqflitePluginPublic_h
#define SqflitePluginPublic_h

#import "SqfliteImportPublic.h"

@class SqfliteDarwinResultSet;

@interface SqflitePlugin : NSObject

/**
 * Returns the shared instance of the plugin.
 * For Unity integration, this allows accessing the plugin instance from the bridge.
 */
+ (instancetype)sharedInstance;

+ (NSArray*)toSqlArguments:(NSArray*)rawArguments;
+ (bool)arrayIsEmpty:(NSArray*)array;
+ (NSMutableDictionary*)resultSetToResults:(SqfliteDarwinResultSet*)resultSet cursorPageSize:(NSNumber*)cursorPageSize;
/**
 * Handles a method call from Flutter or Unity.
 */
- (void)handleMethod:(FlutterMethodCall*)call result:(FlutterResult)result;

@end

#endif // SqflitePluginDef_h
