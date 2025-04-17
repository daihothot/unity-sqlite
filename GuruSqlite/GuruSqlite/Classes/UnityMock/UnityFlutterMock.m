//
//  UnityFlutterMock.m
//  GuruSqlite
//
//  Created for Unity integration with GuruSqlite
//

#import "UnityFlutterMock.h"
#import "../GuruSqliteLog.h"

#pragma mark - FlutterMethodCall Implementation

@implementation FlutterMethodCall {
    NSString *_method;
    NSDictionary *_arguments;
    int _callId;
}

+ (instancetype)methodCallWithMethodName:(NSString *)method arguments:(NSDictionary *)arguments {
    return [[FlutterMethodCall alloc] initWithMethodName:method arguments:arguments callId:0];
}

+ (instancetype)methodCallWithMethodName:(NSString *)method arguments:(NSDictionary *)arguments callId:(int)callId {
    return [[FlutterMethodCall alloc] initWithMethodName:method arguments:arguments callId:callId];
}

- (instancetype)initWithMethodName:(NSString *)method arguments:(NSDictionary *)arguments {
    return [self initWithMethodName:method arguments:arguments callId:0];
}

- (instancetype)initWithMethodName:(NSString *)method arguments:(NSDictionary *)arguments callId:(int)callId {
    LogDebug(@"创建FlutterMethodCall - 方法: %@, callId: %d", method, callId);
    self = [super init];
    if (self) {
        _method = [method copy];
        _arguments = arguments ? [arguments copy] : @{};
        _callId = callId;
        
        if (arguments && arguments.count > 0) {
            LogDebug(@"方法调用参数: %@", arguments);
        } else {
            LogDebug(@"方法调用无参数");
        }
    }
    return self;
}

- (NSString *)method {
    return _method;
}

- (NSDictionary *)arguments {
    return _arguments;
}

- (int)callId {
    return _callId;
}

- (NSString *)description {
    NSString *desc = [NSString stringWithFormat:@"FlutterMethodCall(method: %@, arguments: %@, callId: %d)", _method, _arguments, _callId];
    LogDebug(@"FlutterMethodCall描述: %@", desc);
    return desc;
}

@end

#pragma mark - FlutterError Implementation

@implementation FlutterError {
    NSString *_code;
    NSString *_message;
    id _details;
}

+ (instancetype)errorWithCode:(NSString *)code message:(NSString *)message details:(id)details {
    return [[FlutterError alloc] initWithCode:code message:message details:details];
}

- (instancetype)initWithCode:(NSString *)code message:(NSString *)message details:(id)details {
    LogError(@"创建FlutterError - code: %@, message: %@", code, message);
    self = [super init];
    if (self) {
        _code = [code copy];
        _message = [message copy];
        _details = details;
        
        if (details) {
            if ([details isKindOfClass:[NSDictionary class]] || [details isKindOfClass:[NSArray class]]) {
                LogDebug(@"错误详情: %@", FormatJSONObject(details));
            } else {
                LogDebug(@"错误详情(非JSON): %@", details);
            }
        }
    }
    return self;
}

- (NSString *)code {
    return _code;
}

- (NSString *)message {
    return _message;
}

- (id)details {
    return _details;
}

- (NSString *)description {
    NSString *desc = [NSString stringWithFormat:@"FlutterError(code: %@, message: %@, details: %@)", _code, _message, _details];
    LogDebug(@"FlutterError描述: %@", desc);
    return desc;
}

@end

#pragma mark - FlutterStandardTypedData Implementation

@implementation FlutterStandardTypedData {
    NSData *_data;
}

+ (instancetype)typedDataWithBytes:(NSData *)data {
    return [[FlutterStandardTypedData alloc] initWithData:data];
}

- (instancetype)initWithData:(NSData *)data {
    LogDebug(@"创建FlutterStandardTypedData - 大小: %lu字节", (unsigned long)(data ? data.length : 0));
    self = [super init];
    if (self) {
        _data = [data copy];
    }
    return self;
}

- (NSData *)data {
    return _data;
}

- (NSString *)description {
    NSString *desc = [NSString stringWithFormat:@"FlutterStandardTypedData(length: %lu)", (unsigned long)_data.length];
    LogDebug(@"FlutterStandardTypedData描述: %@", desc);
    return desc;
}

@end
