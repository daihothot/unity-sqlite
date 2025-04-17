//
//  UnityFlutterMock.m
//  GuruSqlite
//
//  Created for Unity integration with GuruSqlite
//

#import "UnityFlutterMock.h"

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
    self = [super init];
    if (self) {
        _method = [method copy];
        _arguments = arguments ? [arguments copy] : @{};
        _callId = callId;
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
    return [NSString stringWithFormat:@"FlutterMethodCall(method: %@, arguments: %@, callId: %d)", _method, _arguments, _callId];
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
    self = [super init];
    if (self) {
        _code = [code copy];
        _message = [message copy];
        _details = details;
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
    return [NSString stringWithFormat:@"FlutterError(code: %@, message: %@, details: %@)", _code, _message, _details];
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
    return [NSString stringWithFormat:@"FlutterStandardTypedData(length: %lu)", (unsigned long)_data.length];
}

@end
