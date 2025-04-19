//
//  GuruSqliteLog.m
//  GuruSqlite
//
//  Created for Unity integration with GuruSqlite
//

#import "GuruSqliteLog.h"
#import <execinfo.h>

// Current log level (default is Info)
static LogLevel currentLogLevel = LogLevelDebug;

// Get call stack information
NSString* GetCallStackInfo(void) {
    NSMutableString *callStackString = [NSMutableString string];
    
    // Get stack trace
    void *callstack[128];
    int frames = backtrace(callstack, 128);
    char **strs = backtrace_symbols(callstack, frames);
    
    if (strs) {
        // Skip first 2 frames (this function and the log function itself)
        for (int i = 2; i < MIN(frames, 7); i++) {
            [callStackString appendFormat:@"\n  %s", strs[i]];
        }
        
        if (frames > 7) {
            [callStackString appendString:@"\n  ..."];
        }
        
        free(strs);
    }
    
    return callStackString;
}

// Log function implementations
void LogError(NSString *format, ...) {
    if (currentLogLevel >= LogLevelError) {
        va_list args;
        va_start(args, format);
        NSString *message = [[NSString alloc] initWithFormat:format arguments:args];
        NSLog(@"[GuruSqlite][ERROR] %@%@", message, GetCallStackInfo());
        va_end(args);
    }
}

void LogWarning(NSString *format, ...) {
    if (currentLogLevel >= LogLevelWarning) {
        va_list args;
        va_start(args, format);
        NSString *message = [[NSString alloc] initWithFormat:format arguments:args];
        NSLog(@"[GuruSqlite][WARNING] %@%@", message, GetCallStackInfo());
        va_end(args);
    }
}

void LogInfo(NSString *format, ...) {
    if (currentLogLevel >= LogLevelInfo) {
        va_list args;
        va_start(args, format);
        NSString *message = [[NSString alloc] initWithFormat:format arguments:args];
        NSLog(@"[GuruSqlite][INFO] %@%@", message, GetCallStackInfo());
        va_end(args);
    }
}

void LogDebug(NSString *format, ...) {
    if (currentLogLevel >= LogLevelDebug) {
        va_list args;
        va_start(args, format);
        NSString *message = [[NSString alloc] initWithFormat:format arguments:args];
        NSLog(@"[GuruSqlite][DEBUG] %@%@", message, GetCallStackInfo());
        va_end(args);
    }
}

// Format JSON object to readable string
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
        return [NSString stringWithFormat:@"<Failed to serialize object: %@>", [obj description]];
    }
}

// Set log level
void SetGuruSqliteLogLevel(int level) {
    if (level >= LogLevelNone && level <= LogLevelDebug) {
        NSString *levelName;
        switch (level) {
            case LogLevelNone: levelName = @"None"; break;
            case LogLevelError: levelName = @"Error"; break;
            case LogLevelWarning: levelName = @"Warning"; break;
            case LogLevelInfo: levelName = @"Info"; break;
            case LogLevelDebug: levelName = @"Debug"; break;
            default: levelName = @"Unknown";
        }
        
        LogLevel oldLevel = currentLogLevel;
        currentLogLevel = level;
        NSLog(@"[GuruSqlite] Log level changed from %d(%@) to: %d(%@)", 
              oldLevel, 
              oldLevel <= LogLevelDebug ? @[@"None", @"Error", @"Warning", @"Info", @"Debug"][oldLevel] : @"Unknown",
              level, 
              levelName);
    } else {
        NSLog(@"[GuruSqlite] Invalid log level: %d, valid range: 0-4", level);
    }
}