//
//  UnityFlutterBridge.m
//  GuruSqlite
//
//  Created for Unity integration with GuruSqlite
//

#import "UnityFlutterMock.h"
#import "../SqflitePlugin.h"
#import "../GuruSqliteLog.h"

// Store result callbacks by callId to prevent them from being deallocated
static NSMutableDictionary<NSNumber*, void(^)(id)> *resultCallbacks;

// Initialize the result callbacks dictionary
__attribute__((constructor))
static void InitializeResultCallbacks(void) {
    LogInfo(@"初始化回调字典");
    resultCallbacks = [NSMutableDictionary dictionary];
}

// 包裹结果数据，添加callId（接受任意对象）
const char* WrapObjectResultWithCallId(int callId, id data) {
    LogDebug(@"包裹结果数据, callId: %d, 数据类型: %@", callId, [data class]);
    
    // 创建包含callId和data的字典
    NSDictionary *wrappedResult = @{
        @"callId": @(callId),
        @"data": data ?: [NSNull null]
    };
    
    // 序列化为JSON
    NSError *jsonError;
    NSData *jsonData = [NSJSONSerialization dataWithJSONObject:wrappedResult options:0 error:&jsonError];
    if (jsonData) {
        NSString *jsonString = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];
        LogDebug(@"包裹后的结果: %@", jsonString);
        return [jsonString UTF8String];
    } else {
        LogError(@"包裹结果JSON序列化失败: %@", jsonError);
        // 使用直接的字符串格式化返回结果
        NSString *errorStr = [NSString stringWithFormat:@"{\"callId\":%d,\"data\":\"Error wrapping result\"}", callId];
        return [errorStr UTF8String];
    }
}

// Convert C result callback to Objective-C block
FlutterResult CreateFlutterResultBlock(int callId,  MethodResultCallback onMethodResultCallback) {
    LogDebug(@"创建结果回调Block, callId: %d", callId);
    
    // Create a block that will call the C function pointer
    void (^resultBlock)(id) = ^(id result) {
        LogDebug(@"处理回调结果 callId: %d, 结果类型: %@", callId, NSStringFromClass([result class]));
        if (onMethodResultCallback) {
            // Handle different result types
            if ([result isKindOfClass:[FlutterError class]]) {
                // For FlutterError, we need to convert it to a dictionary
                FlutterError *error = (FlutterError *)result;
                NSDictionary *errorDict = @{
                    @"code": error.code ?: @"",
                    @"message": error.message ?: @"",
                    @"details": error.details ?: [NSNull null]
                };
                
                LogError(@"回调返回错误: code=%@, message=%@", error.code, error.message);
                
                // Convert dictionary to JSON
                NSError *jsonError;
                NSData *jsonData = [NSJSONSerialization dataWithJSONObject:errorDict options:0 error:&jsonError];
                if (jsonData) {
                    LogDebug(@"错误字典准备完成");
                    onMethodResultCallback(WrapObjectResultWithCallId(callId, errorDict));
                } else {
                    LogError(@"错误JSON序列化失败: %@", jsonError);
                    NSString *errorMsg = @"Error serializing FlutterError";
                    onMethodResultCallback(WrapObjectResultWithCallId(callId, errorMsg));
                }
            } 
            else if ([result isKindOfClass:[NSString class]]) {
                // For strings, pass them directly
                LogDebug(@"回调返回字符串: %@", result);
                onMethodResultCallback(WrapObjectResultWithCallId(callId, result));
            }
            else if ([result isKindOfClass:[NSNumber class]]) {
                // For numbers, pass them directly
                LogDebug(@"回调返回数字: %@", result);
                onMethodResultCallback(WrapObjectResultWithCallId(callId, result));
            }
            else if ([result isKindOfClass:[NSDictionary class]] || [result isKindOfClass:[NSArray class]]) {
                // For dictionaries and arrays, use WrapObjectResultWithCallId
                NSString *type = [result isKindOfClass:[NSDictionary class]] ? @"字典" : @"数组";
                
                // Debug日志输出格式化的JSON
                NSError *printError;
                NSData *printData = [NSJSONSerialization dataWithJSONObject:result options:NSJSONWritingPrettyPrinted error:&printError];
                if (printData) {
                    NSString *printString = [[NSString alloc] initWithData:printData encoding:NSUTF8StringEncoding];
                    LogDebug(@"回调返回%@: %@", type, printString);
                } 
                onMethodResultCallback(WrapObjectResultWithCallId(callId, result));
            }
            else if (result == nil || result == [NSNull null]) {
                // For nil or NSNull, return null string
                LogDebug(@"回调返回null");
                onMethodResultCallback(WrapObjectResultWithCallId(callId, [NSNull null]));
            }
            else {
                // For other types, convert to description
                LogDebug(@"回调返回未知类型: %@", [result class]);
                onMethodResultCallback(WrapObjectResultWithCallId(callId, [result description]));
            }
        }
        
        // Remove the callback from the dictionary after it's been called
        if (callId != 0) {
            @synchronized(resultCallbacks) {
                LogDebug(@"移除回调, callId: %d", callId);
                [resultCallbacks removeObjectForKey:@(callId)];
            }
        }
    };
    
    // Store the block in the dictionary to prevent it from being deallocated
    if (callId != 0) {
        @synchronized(resultCallbacks) {
            LogDebug(@"存储回调, callId: %d", callId);
            resultCallbacks[@(callId)] = resultBlock;
        }
    }
    
    return resultBlock;
}

// The C function that will be called from Unity
void InvokeMethod(int callId, const char* methodName, const char* jsonArguments, MethodResultCallback onMethodResultCallback) {
    @autoreleasepool {
        // Convert C strings to NSString
        NSString *method = [NSString stringWithUTF8String:methodName];
        NSString *argsJson = [NSString stringWithUTF8String:jsonArguments];
        
        LogInfo(@"调用方法开始 ==> callId: %d, 方法: %@", callId, method);
        LogDebug(@"参数 JSON: %@", argsJson);
        
        // Parse JSON arguments
        NSError *jsonError;
        NSDictionary *arguments = nil;
        
        if (argsJson && ![argsJson isEqualToString:@""]) {
            NSData *jsonData = [argsJson dataUsingEncoding:NSUTF8StringEncoding];
            arguments = [NSJSONSerialization JSONObjectWithData:jsonData options:0 error:&jsonError];
            
            if (jsonError) {
                LogError(@"解析JSON参数错误: %@", jsonError);
                if (onMethodResultCallback) {
                    NSString *errorMsg = [NSString stringWithFormat:@"Error parsing JSON arguments: %@", jsonError.localizedDescription];
                    onMethodResultCallback(WrapObjectResultWithCallId(callId, errorMsg));
                }
                return;
            } else {
                LogDebug(@"JSON参数解析成功");
            }
        } else {
            // Empty arguments
            arguments = @{};
        }
        
        // Create FlutterMethodCall
        FlutterMethodCall *call = [FlutterMethodCall methodCallWithMethodName:method arguments:arguments callId:callId];
        LogDebug(@"创建方法调用对象: %@", call);
        
        // Create FlutterResult block
        FlutterResult result = CreateFlutterResultBlock(callId, onMethodResultCallback);
        
        // Get the plugin instance
        SqflitePlugin *plugin = [SqflitePlugin sharedInstance];
        if (!plugin) {
            LogError(@"SqflitePlugin实例未找到");
            if (onMethodResultCallback) {
                onMethodResultCallback(WrapObjectResultWithCallId(callId, @"SqflitePlugin instance not found"));
            }
            return;
        }
        
        // Call the plugin's handleMethod
        LogInfo(@"调用插件方法: %@", call.method);
        @try {
            [plugin handleMethod:call result:result];
            LogInfo(@"调用方法完成 <== callId: %d, 方法: %@", callId, method);
        } @catch (NSException *exception) {
            LogError(@"调用方法异常 - 名称: %@, 原因: %@", exception.name, exception.reason);
            if (exception.userInfo) {
                LogDebug(@"异常详情: %@", exception.userInfo);
            }
            if (onMethodResultCallback) {
                NSString *errorMessage = [NSString stringWithFormat:@"Exception: %@ - %@", exception.name, exception.reason];
                onMethodResultCallback(WrapObjectResultWithCallId(callId, errorMessage));
            }
        } @finally {
            LogDebug(@"方法调用处理完成");
        }
    }
}

// This function is now defined in GuruSqliteLog.m

