//
//  UnityFlutterBridge.m
//  GuruSqlite
//
//  Created for Unity integration with GuruSqlite
//

#import "UnityFlutterMock.h"
#import "../SqflitePlugin.h"
#import "../GuruSqliteLog.h"

// 格式化JSON对象为易读字符串
NSString* FormatJSONObject(id obj) {
    if (!obj) return @"null";
    if ([obj isKindOfClass:[NSString class]]) return obj;
    
    NSError *error;
    NSData *jsonData = [NSJSONSerialization dataWithJSONObject:obj
                                                       options:NSJSONWritingPrettyPrinted
                                                         error:&error];
    if (jsonData) {
        NSString *jsonString = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];
        return jsonString;
    } else {
        return [NSString stringWithFormat:@"<无法序列化的对象: %@>", [obj description]];
    }
}

// Store result callbacks by callId to prevent them from being deallocated
static NSMutableDictionary<NSNumber*, void(^)(id)> *resultCallbacks;

// Initialize the result callbacks dictionary
__attribute__((constructor))
static void InitializeResultCallbacks(void) {
    LogInfo(@"初始化回调字典");
    resultCallbacks = [NSMutableDictionary dictionary];
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
                    NSString *jsonString = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];
                    LogDebug(@"错误JSON序列化成功: %@", jsonString);
                    onMethodResultCallback((__bridge const void *)(jsonString));
                } else {
                    LogError(@"错误JSON序列化失败: %@", jsonError);
                    onMethodResultCallback("Error serializing FlutterError");
                }
            } 
            else if ([result isKindOfClass:[NSString class]]) {
                // For strings, pass them directly
                LogDebug(@"回调返回字符串: %@", result);
                onMethodResultCallback((__bridge const void *)(result));
            }
            else if ([result isKindOfClass:[NSNumber class]]) {
                // For numbers, convert to string
                LogDebug(@"回调返回数字: %@", result);
                onMethodResultCallback((__bridge const void *)([result stringValue]));
            }
            else if ([result isKindOfClass:[NSDictionary class]] || [result isKindOfClass:[NSArray class]]) {
                // For dictionaries and arrays, convert to JSON
                NSString *type = [result isKindOfClass:[NSDictionary class]] ? @"字典" : @"数组";
                
                // Debug日志输出格式化的JSON
                NSError *printError;
                NSData *printData = [NSJSONSerialization dataWithJSONObject:result options:NSJSONWritingPrettyPrinted error:&printError];
                if (printData) {
                    NSString *printString = [[NSString alloc] initWithData:printData encoding:NSUTF8StringEncoding];
                    LogDebug(@"回调返回%@: %@", type, printString);
                }
                
                // 实际序列化用于回调
                NSError *jsonError;
                NSData *jsonData = [NSJSONSerialization dataWithJSONObject:result options:0 error:&jsonError];
                if (jsonData) {
                    NSString *jsonString = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];
                    onMethodResultCallback((__bridge const void *)(jsonString));
                } else {
                    LogError(@"对象JSON序列化失败: %@", jsonError);
                    onMethodResultCallback("Error serializing result to JSON");
                }
            }
            else if (result == nil || result == [NSNull null]) {
                // For nil or NSNull, return null string
                LogDebug(@"回调返回null");
                onMethodResultCallback("null");
            }
            else {
                // For other types, convert to description
                LogDebug(@"回调返回未知类型: %@", [result class]);
                onMethodResultCallback((__bridge const void *)([result description]));
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
                    onMethodResultCallback((__bridge const void *)(errorMsg));
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
                onMethodResultCallback("SqflitePlugin instance not found");
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
                onMethodResultCallback((__bridge const void *)(errorMessage));
            }
        } @finally {
            LogDebug(@"方法调用处理完成");
        }
    }
}

// This function is now defined in GuruSqliteLog.m
