//
//  GuruSqliteLog.h
//  GuruSqlite
//
//  Created for Unity integration with GuruSqlite
//

#ifndef GuruSqliteLog_h
#define GuruSqliteLog_h

#import <Foundation/Foundation.h>

// Log level definitions
typedef enum {
    LogLevelNone = 0,   // No logging
    LogLevelError = 1,  // Only errors
    LogLevelWarning = 2,// Warnings and errors
    LogLevelInfo = 3,   // Info, warnings, and errors
    LogLevelDebug = 4   // All debug information
} LogLevel;

// Log functions
void LogError(NSString *format, ...);
void LogWarning(NSString *format, ...);
void LogInfo(NSString *format, ...);
void LogDebug(NSString *format, ...);

// Helper functions
NSString* FormatJSONObject(id obj);
NSString* GetCallStackInfo(void);

// Set log level
void SetGuruSqliteLogLevel(int level);

#endif /* GuruSqliteLog_h */