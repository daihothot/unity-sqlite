using System.Linq;
using System.Threading;

namespace Guru.SDK.Framework.Utils.Log
{
    using System;
    using System.Collections.Generic;

    public static class LogLevel
    {
        public const int Verbose = 0;
        public const int Debug = 1;
        public const int Info = 2;
        public const int Warning = 3;
        public const int Error = 4;
        public const int Fatal = 5;
        public const int Nothing = 0xFFFF;

        public static readonly string[] LevelTags = { " V ", " D ", " I ", " W ", " E ", " F " };
    }

    public class LogRecord
    {
        public int Sequence { get; }
        public DateTime Time { get; }
        public int Level { get; }
        public string Tag { get; }
        public string Message { get; }

        public LogRecord(int sequence, DateTime time, int level, string tag, string message)
        {
            Sequence = sequence;
            Time = time;
            Level = level;
            Tag = tag;
            Message = message;
        }
    }

    public static class Log
    {
        private static int _recordCount = 0;
        private static readonly Queue<LogRecord> LatestLogRecords = new(2000);
        private static bool _listening = false;
        private static readonly List<Action<LogRecord>> LogTrackers = new();
        private static string _appName = "GAME";
        private static int _persistentLevel = LogLevel.Debug;
        const string GuruSdkLogName = "gurusdk";
        public static readonly Queue<string> PendingPersistentMsg = new Queue<string>();

        private static Action<string> persistentLog = AddPendingPersistentMsg;

        // TODO: 这块需要在 MonoBehaviour启动后,需要验证
        // private static readonly string PersistentLogPath = Application.persistentDataPath + "/log.txt";
        private static readonly object FileLock = new object();

        /// <summary>
        /// 初始化日志系统
        /// </summary>
        public static void Init(string appName, int persistentLogFileSize = 1024 * 1024 * 10,
            int persistentLogCount = 7,
            int persistentLevel = LogLevel.Debug)
        {
            _appName = appName;
            _persistentLevel = persistentLevel;
            _persistentLevel = persistentLevel;
            if (persistentLevel == LogLevel.Nothing) return;
            // OfflineLog.CreateLogger(
            //     logName: GuruSdkLogName,
            //     fileSizeLimit: persistentLogFileSize,
            //     fileCount: persistentLogCount);
            persistentLog = WriteToPersistentLog;
            WritePendingPersistentMsg();
        }

        public static void SetListening(bool listening)
        {
            _listening = listening;
        }

        public static void AddTracker(Action<LogRecord> tracker)
        {
            LogTrackers.Add(tracker);
        }

        public static void RemoveTracker(Action<LogRecord> tracker)
        {
            LogTrackers.Remove(tracker);
        }

        public static IEnumerable<LogRecord> DumpRecords(string filterTag = null)
        {
            return LatestLogRecords.Where(record =>
                filterTag == null || record.Tag == filterTag || record.Tag == _appName);
        }

        private static void RecordLog(LogRecord record)
        {
            if (LatestLogRecords.Count >= 2000)
            {
                LatestLogRecords.Dequeue();
            }

            LatestLogRecords.Enqueue(record);

            foreach (var tracker in LogTrackers)
            {
                tracker(record);
            }
        }

        private static void WriteToPersistentLog(string message)
        {
            // OfflineLog.LogMessage(GuruSdkLogName, message);
        }

        private static void AddPendingPersistentMsg(string message)
        {
            PendingPersistentMsg.Enqueue(message);
        }
        
        private static void WritePendingPersistentMsg()
        {
            while (PendingPersistentMsg.Count > 0)
            {
                WriteToPersistentLog(PendingPersistentMsg.Dequeue());
            }
        }

        private static void LogInternal(int level, string message, string tag, Exception exception = null)
        {
            _recordCount++;
            var now = DateTime.Now;
            var logRecord = new LogRecord(_recordCount, now, level, tag, message);

            RecordLog(logRecord);

            // Log to Unity console
            var levelTag = LogLevel.LevelTags[level];
            var logMessage = $"[{_appName}] [{levelTag}] [{tag}] [{Thread.CurrentThread.Name}-{Thread.CurrentThread.ManagedThreadId}] {message}";
            if (exception != null)
            {
                logMessage += $"\nException: {exception}";
            }

            switch (level)
            {
                case LogLevel.Verbose:
                case LogLevel.Debug:
                case LogLevel.Info:
                    UnityEngine.Debug.Log(logMessage);
                    break;
                case LogLevel.Warning:
                    UnityEngine.Debug.LogWarning(logMessage);
                    break;
                case LogLevel.Error:
                case LogLevel.Fatal:
                    UnityEngine.Debug.LogError(logMessage);
                    break;
            }

            // Write to persistent log file if level >= persistent level
            if (level >= _persistentLevel)
            {
                persistentLog(logMessage);
            }
        }

        public static void Verbose(string message, string tag = null, Exception exception = null)
        {
            LogInternal(LogLevel.Verbose, message, tag, exception);
        }

        public static void Debug(string message, string tag = null, Exception exception = null)
        {
            LogInternal(LogLevel.Debug, message, tag, exception);
        }

        public static void Info(string message, string tag = null, Exception exception = null)
        {
            LogInternal(LogLevel.Info, message, tag, exception);
        }

        public static void Warning(string message, string tag = null, Exception exception = null)
        {
            LogInternal(LogLevel.Warning, message, tag, exception);
        }

        public static void Error(string message, string tag = null, Exception exception = null)
        {
            LogInternal(LogLevel.Error, message, tag, exception);
        }

        public static void Fatal(string message, string tag = null, Exception exception = null)
        {
            LogInternal(LogLevel.Fatal, message, tag, exception);
        }

        public static void V(string message, string tag = null, Exception exception = null)
        {
            Verbose(message, tag, exception);
        }

        public static void D(string message, string tag = null, Exception exception = null, bool syncFirebase = false)
        {
            Debug(message, tag, exception);
        }

        public static void I(string message, string tag = null, Exception exception = null, bool syncFirebase = false)
        {
            Info(message, tag, exception);
        }

        public static void W(string message, string tag = null, Exception exception = null, bool syncFirebase = false)
        {
            Warning(message, tag, exception);
        }

        public static void E(string message, string tag = null, Exception exception = null, bool syncFirebase = false)
        {
            Error(message, tag, exception);
        }
    }
}