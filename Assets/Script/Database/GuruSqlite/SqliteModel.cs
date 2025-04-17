#nullable enable
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Guru.SDK.Framework.Utils.Log;
using Newtonsoft.Json;

namespace GuruSqlite
{
   // Unity端C#版本的MethodCall类
    public class MethodCall
    {
        public readonly string Method;
        public readonly object Arguments;

        public MethodCall(string method, object arguments)
        {
            Method = method;
            Arguments = arguments;
        }

        public T? GetArgument<T>(string key)
        {
            if (Arguments is not Dictionary<string, object> dict) return default(T);
            if (dict.TryGetValue(key, out var value))
            {
                return (T)value;
            }

            return default(T);
        }

        public bool HasArgument(string key)
        {
            return Arguments is Dictionary<string, object> dict && dict.ContainsKey(key);
        }

        public Dictionary<string, object> ToMap()
        {
            var map = new Dictionary<string, object>
            {
                { "method", Method }
            };

            map["arguments"] = Arguments;

            return map;
        }

        public static MethodCall FromMap(Dictionary<string, object> map)
        {
            var method = (string)map["method"];
            var arguments = map.GetValueOrDefault("arguments");
            return new MethodCall(method, arguments);
        }
    }

    public interface IResultCallback
    {
        void OnResult(object? result);
        void OnError(string code, string message, object? details);
        void OnNotImplemented();
    }

    public class SqliteResultCallback<T> : IResultCallback
    {
        internal readonly UniTaskCompletionSource<T> Tcs = new();

        public void OnResult(object? result)
        {
            Log.I($"OnResult: ExpectType: {typeof(T)} ResultType:{result?.GetType()} Result:{JsonConvert.SerializeObject(result)}");
            
            switch (result)
            {
                case null:
                    Tcs.TrySetResult(default!);
                    return;
                case T typedResult:
                    Tcs.TrySetResult(typedResult);
                    break;
                default:
                    Log.W($"SqliteResultCallback OnResult: Type mismatch. ResultType:{result?.GetType()} ExpectType: {typeof(T)}");
                    Tcs.TrySetException(new Exception($"Format error: {result} ExpectType: {typeof(T)}"));
                    break;
            }
        }

        public void OnError(string code, string message, object? details)
        {
            Log.W($"OnError: {code} {message} {details}");

            Tcs.TrySetException(new Exception($"{code}: {message}"));
        }

        public void OnNotImplemented()
        {
            Log.W($"OnNotImplemented");
            Tcs.TrySetException(new NotImplementedException());
        }
    }
    
    public class SqliteResult
    {
        private readonly IResultCallback callback;

        public SqliteResult(IResultCallback callback)
        {
            this.callback = callback;
        }

        private void Success(object result)
        {
            callback.OnResult(result);
        }

        private void Error(string errorCode, string errorMessage, object errorDetails)
        {
            callback.OnError(errorCode, errorMessage, errorDetails);
        }

        public void NotImplemented()
        {
            callback.OnNotImplemented();
        }

        public void SqliteQuerySuccess(List<Dictionary<string, object>> rows)
        {
            var result = new Dictionary<string, object>
            {
                { "rows", rows },
                { "rowCount", rows.Count }
            };
            Success(result);
        }

        public void SqliteExecuteSuccess(int rowsAffected, long? insertId)
        {
            var result = new Dictionary<string, object>
            {
                { "rowsAffected", rowsAffected }
            };

            if (insertId.HasValue)
            {
                result["insertId"] = insertId.Value;
            }

            Success(result);
        }

        public void SqliteError(string code, string message)
        {
            Error("sqlite_error", message, code);
        }

        public void SqliteTransactionResult(bool successful)
        {
            var result = new Dictionary<string, object>
            {
                { "transactionSuccessful", successful }
            };
            Success(result);
        }
    }
}