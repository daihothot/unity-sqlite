//
//  UnityFlutterMock.h
//  GuruSqlite
//
//  Created for Unity integration with GuruSqlite
//

#ifndef UnityFlutterMock_h
#define UnityFlutterMock_h

#import <Foundation/Foundation.h>

/**
 * Mock implementation of Flutter's FlutterMethodCall for Unity integration.
 * This class mimics the behavior of Flutter's FlutterMethodCall to allow Unity
 * to interact with the native SQLite plugin without modifying the plugin code.
 */
@interface FlutterMethodCall : NSObject

/**
 * The method name to be called.
 */
@property (nonatomic, readonly, copy) NSString *method;

/**
 * The arguments passed to the method.
 */
@property (nonatomic, readonly, copy) NSDictionary *arguments;

/**
 * A unique identifier for this method call.
 */
@property (nonatomic, readonly) int callId;

/**
 * Creates a new FlutterMethodCall with the specified method name and arguments.
 */
+ (instancetype)methodCallWithMethodName:(NSString *)method arguments:(NSDictionary *)arguments;

/**
 * Creates a new FlutterMethodCall with the specified method name, arguments, and callId.
 */
+ (instancetype)methodCallWithMethodName:(NSString *)method arguments:(NSDictionary *)arguments callId:(int)callId;

/**
 * Initializes a new FlutterMethodCall with the specified method name and arguments.
 */
- (instancetype)initWithMethodName:(NSString *)method arguments:(NSDictionary *)arguments;

/**
 * Initializes a new FlutterMethodCall with the specified method name, arguments, and callId.
 */
- (instancetype)initWithMethodName:(NSString *)method arguments:(NSDictionary *)arguments callId:(int)callId;

@end

/**
 * Mock implementation of Flutter's FlutterError for Unity integration.
 * This class mimics the behavior of Flutter's FlutterError to allow Unity
 * to interact with the native SQLite plugin without modifying the plugin code.
 */
@interface FlutterError : NSObject

/**
 * The error code.
 */
@property (nonatomic, readonly, copy) NSString *code;

/**
 * The error message.
 */
@property (nonatomic, readonly, copy) NSString *message;

/**
 * Additional error details.
 */
@property (nonatomic, readonly, strong) id details;

/**
 * Creates a new FlutterError with the specified code, message, and details.
 */
+ (instancetype)errorWithCode:(NSString *)code
                      message:(NSString *)message
                      details:(id)details;

/**
 * Initializes a new FlutterError with the specified code, message, and details.
 */
- (instancetype)initWithCode:(NSString *)code
                     message:(NSString *)message
                     details:(id)details;

@end

/**
 * Mock implementation of Flutter's FlutterStandardTypedData for Unity integration.
 * This class mimics the behavior of Flutter's FlutterStandardTypedData to allow Unity
 * to interact with the native SQLite plugin without modifying the plugin code.
 */
@interface FlutterStandardTypedData : NSObject

/**
 * The data contained in this object.
 */
@property (nonatomic, readonly, copy) NSData *data;

/**
 * Creates a new FlutterStandardTypedData with the specified data.
 */
+ (instancetype)typedDataWithBytes:(NSData *)data;

/**
 * Initializes a new FlutterStandardTypedData with the specified data.
 */
- (instancetype)initWithData:(NSData *)data;

@end

/**
 * Type definition for FlutterResult callback.
 * This is a block that takes an object parameter and returns void.
 */
typedef void (^FlutterResult)(id _Nullable result);

/**
 * Unity bridge function to invoke a method on the plugin.
 * This function creates a FlutterMethodCall from the parameters and calls the plugin's handleMethod.
 *
 * @param callId The unique identifier for this method call.
 * @param methodName The name of the method to call.
 * @param jsonArguments The arguments as a JSON string.
 * @param resultCallback A function pointer to call with the result.
 */
#ifdef __cplusplus
extern "C" {
#endif

typedef void (*MethodResultCallback)(const char* result);

void invokeMethod(int callId, const char* methodName, const char* jsonArguments, MethodResultCallback onMethodResultCallback);

#ifdef __cplusplus
}
#endif

#endif /* UnityFlutterMock_h */
