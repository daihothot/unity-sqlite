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
}