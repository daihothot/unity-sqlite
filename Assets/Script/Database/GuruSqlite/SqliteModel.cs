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
            
            // 处理null情况
            if (result == null)
            {
                Tcs.TrySetResult(default!);
                return;
            }
            
            // // 如果已经是T类型，直接返回
            // if (result is T typedResult)
            // {
            //     Tcs.TrySetResult(typedResult);
            //     return;
            // }
            
            // 确保结果是字符串类型
            string stringValue = result.ToString();
            
            try
            {
                // 根据期望的类型进行不同的解析
                if (typeof(T) == typeof(int))
                {
                    // 尝试解析为int
                    if (int.TryParse(stringValue, out int intValue))
                    {
                        Tcs.TrySetResult((T)(object)intValue);
                        return;
                    }
                }
                else if (typeof(T) == typeof(bool))
                {
                    // 尝试解析为bool
                    if (bool.TryParse(stringValue, out bool boolValue))
                    {
                        Tcs.TrySetResult((T)(object)boolValue);
                        return;
                    }
                }
                else if (typeof(T) == typeof(string))
                {
                    // 直接作为字符串返回
                    Tcs.TrySetResult((T)(object)stringValue);
                    return;
                }
                else
                {
                    // 尝试解析为JSON字典
                    if (stringValue.StartsWith("{") && stringValue.EndsWith("}"))
                    {
                        try
                        {
                            var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(stringValue);
                            if (dict != null)
                            {
                                Tcs.TrySetResult((T)(object)dict);
                                return;
                            }
                        }
                        catch (JsonException)
                        {
                            // JSON解析失败，记录日志
                            Log.W($"SqliteResultCallback OnResult: Failed to parse JSON string: {stringValue}");
                        }
                    }
                    // 如果不是有效的JSON或解析失败，报告错误
                    Tcs.TrySetException(new Exception($"Cannot parse to Dictionary: {stringValue}"));
                    return;
                }
                
                // // 如果没有匹配的解析方式，报告类型不匹配
                // Log.W($"SqliteResultCallback OnResult: Type mismatch. Result:{stringValue} ExpectType: {typeof(T)}");
                // Tcs.TrySetException(new Exception($"Format error: {stringValue} ExpectType: {typeof(T)}"));
            }
            catch (Exception ex)
            {
                Log.W($"SqliteResultCallback OnResult: Exception: {ex.Message}");
                Tcs.TrySetException(ex);
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