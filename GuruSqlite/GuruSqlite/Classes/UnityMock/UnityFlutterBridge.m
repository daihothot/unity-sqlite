//
//  UnityFlutterBridge.m
//  GuruSqlite
//
//  Created for Unity integration with GuruSqlite
//

#import "UnityFlutterMock.h"
#import "../SqflitePlugin.h"

// Store result callbacks by callId to prevent them from being deallocated
static NSMutableDictionary<NSNumber*, void(^)(id)> *resultCallbacks;

// Initialize the result callbacks dictionary
__attribute__((constructor))
static void InitializeResultCallbacks(void) {
    resultCallbacks = [NSMutableDictionary dictionary];
}

// Convert C result callback to Objective-C block
FlutterResult CreateFlutterResultBlock(int callId,  MethodResultCallback onMethodResultCallback) {
    // Create a block that will call the C function pointer
    void (^resultBlock)(id) = ^(id result) {
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
                
                // Convert dictionary to JSON
                NSError *jsonError;
                NSData *jsonData = [NSJSONSerialization dataWithJSONObject:errorDict options:0 error:&jsonError];
                if (jsonData) {
                    NSString *jsonString = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];
                    onMethodResultCallback((__bridge const void *)(jsonString));
                } else {
                    onMethodResultCallback("Error serializing FlutterError");
                }
            } 
            else if ([result isKindOfClass:[NSString class]]) {
                // For strings, pass them directly
                onMethodResultCallback((__bridge const void *)(result));
            }
            else if ([result isKindOfClass:[NSNumber class]]) {
                // For numbers, convert to string
                onMethodResultCallback((__bridge const void *)([result stringValue]));
            }
            else if ([result isKindOfClass:[NSDictionary class]] || [result isKindOfClass:[NSArray class]]) {
                // For dictionaries and arrays, convert to JSON
                NSError *jsonError;
                NSData *jsonData = [NSJSONSerialization dataWithJSONObject:result options:0 error:&jsonError];
                if (jsonData) {
                    NSString *jsonString = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];
                    onMethodResultCallback((__bridge const void *)(jsonString));
                } else {
                    onMethodResultCallback("Error serializing result to JSON");
                }
            }
            else if (result == nil || result == [NSNull null]) {
                // For nil or NSNull, return null string
                onMethodResultCallback("null");
            }
            else {
                // For other types, convert to description
                onMethodResultCallback((__bridge const void *)([result description]));
            }
        }
        
        // Remove the callback from the dictionary after it's been called
        if (callId != 0) {
            @synchronized(resultCallbacks) {
                [resultCallbacks removeObjectForKey:@(callId)];
            }
        }
    };
    
    // Store the block in the dictionary to prevent it from being deallocated
    if (callId != 0) {
        @synchronized(resultCallbacks) {
            resultCallbacks[@(callId)] = resultBlock;
        }
    }
    
    return resultBlock;
}

// The C function that will be called from Unity
void invokeMethod(int callId, const char* methodName, const char* jsonArguments, MethodResultCallback onMethodResultCallback) {
    @autoreleasepool {
        // Convert C strings to NSString
        NSString *method = [NSString stringWithUTF8String:methodName];
        NSString *argsJson = [NSString stringWithUTF8String:jsonArguments];
        
        // Parse JSON arguments
        NSError *jsonError;
        NSDictionary *arguments = nil;
        
        if (argsJson && ![argsJson isEqualToString:@""]) {
            NSData *jsonData = [argsJson dataUsingEncoding:NSUTF8StringEncoding];
            arguments = [NSJSONSerialization JSONObjectWithData:jsonData options:0 error:&jsonError];
            
            if (jsonError) {
                NSLog(@"Error parsing JSON arguments: %@", jsonError);
                if (onMethodResultCallback) {
                    NSString *errorMsg = [NSString stringWithFormat:@"Error parsing JSON arguments: %@", jsonError.localizedDescription];
                    onMethodResultCallback((__bridge const void *)(errorMsg));
                }
                return;
            }
        } else {
            // Empty arguments
            arguments = @{};
        }
        
        // Create FlutterMethodCall
        FlutterMethodCall *call = [FlutterMethodCall methodCallWithMethodName:method arguments:arguments callId:callId];
        
        // Create FlutterResult block
        FlutterResult result = CreateFlutterResultBlock(callId, onMethodResultCallback);
        
        // Get the plugin instance
        SqflitePlugin *plugin = [SqflitePlugin sharedInstance];
        if (!plugin) {
            NSLog(@"SqflitePlugin instance not found");
            if (onMethodResultCallback) {
                onMethodResultCallback("SqflitePlugin instance not found");
            }
            return;
        }
        
        // Call the plugin's handleMethod
        [plugin handleMethod:call result:result];
    }
}
