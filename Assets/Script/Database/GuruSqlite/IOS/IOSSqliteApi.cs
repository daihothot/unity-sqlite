#nullable enable
#if UNITY_IOS
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using AOT;
using Cysharp.Threading.Tasks;
using Guru.SDK.Framework.Utils.Log;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace GuruSqlite
{
    internal class MethodRequest
    {
        [JsonProperty("id")]
        internal int CallId { get; private init; }

        internal MethodCall MethodCall { get; private init; }
        internal SqliteResultCallback<object> MethodResult;

        public MethodRequest(int callId, MethodCall methodCall)
        {
            CallId = callId;
            MethodCall = methodCall;
            MethodResult = new SqliteResultCallback<object>();
        }
    }

    /// <summary>
    /// IOSSqliteResult的自定义JSON转换器
    /// </summary>
    internal class IOSSqliteResultConverter : JsonConverter<IOSSqliteResult>
    {
        public override IOSSqliteResult ReadJson(JsonReader reader, Type objectType, IOSSqliteResult? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            try
            {
                // 跳过null值
                if (reader.TokenType == JsonToken.Null)
                    throw new JsonSerializationException($"预期会收到 IOSSqliteResult 的 StartObject 标记，但收到的是: {reader.TokenType}");

                Log.D($"开始解析IOSSqliteResult: TokenType={reader.TokenType}");

                // 确保开始对象
                if (reader.TokenType != JsonToken.StartObject)
                {
                    Log.E($"解析IOSSqliteResult失败: 预期StartObject, 实际为{reader.TokenType}");
                    return new IOSSqliteResult(-1, null);
                }

                int callId = -1;
                object? data = null;

                // 读取对象属性
                while (reader.Read() && reader.TokenType != JsonToken.EndObject)
                {
                    if (reader.TokenType != JsonToken.PropertyName)
                        continue;

                    string propertyName = reader.Value != null ? reader.Value.ToString() : string.Empty;
                    Log.D($"读取属性: {propertyName}");

                    // 读取下一个token (属性值)
                    reader.Read();

                    if (propertyName == "callId")
                    {
                        if (reader.TokenType == JsonToken.Integer)
                        {
                            callId = Convert.ToInt32(reader.Value);
                            Log.D($"解析callId: {callId}");
                        }
                        else if (reader.TokenType == JsonToken.String && reader.Value != null)
                        {
                            // 尝试从字符串转换为数字
                            if (int.TryParse(reader.Value.ToString(), out int parsedId))
                            {
                                callId = parsedId;
                                Log.D($"从字符串解析callId: {callId}");
                            }
                        }
                    }
                    else if (propertyName == "data")
                    {
                        data = ReadValue(reader);
                        Log.D($"解析data完成: 类型={data?.GetType().Name ?? "null"}");
                    }
                    else
                    {
                        // 跳过其他未知属性
                        Log.D($"跳过未知属性: {propertyName}");
                    }
                }

                return new IOSSqliteResult(callId, data);
            }
            catch (Exception ex)
            {
                Log.E($"ReadJson异常: {ex.Message}");
                return new IOSSqliteResult(-1, null);
            }
        }

        public override void WriteJson(JsonWriter writer, IOSSqliteResult? value, JsonSerializer serializer)
        {
            // 不需要实现序列化
            throw new NotImplementedException();
        }

        /// <summary>
        /// 读取并转换JSON值为相应的C#对象
        /// </summary>
        private object ReadValue(JsonReader reader)
        {
            switch (reader.TokenType)
            {
                case JsonToken.StartObject:
                    {
                        var objResult = ReadObject(reader);
                        Log.D($"转换对象: {objResult.Count}个属性");
                        return objResult;
                    }

                case JsonToken.StartArray:
                    {
                        var arrResult = ReadArray(reader);
                        Log.D($"转换数组: {arrResult.Count}个项");
                        return arrResult;
                    }

                case JsonToken.Integer:
                    {
                        var intResult = Convert.ToInt64(reader.Value);
                        Log.D($"转换整数: {intResult}");
                        return intResult;
                    }

                case JsonToken.Float:
                    {
                        var floatResult = Convert.ToDouble(reader.Value);
                        Log.D($"转换浮点数: {floatResult}");
                        return floatResult;
                    }

                case JsonToken.Boolean:
                    {
                        var boolResult = Convert.ToBoolean(reader.Value);
                        Log.D($"转换布尔值: {boolResult}");
                        return boolResult;
                    }

                case JsonToken.String:
                    {
                        var strResult = reader.Value != null ? reader.Value.ToString() : string.Empty;
                        Log.D($"转换字符串: \"{strResult}\"");
                        return strResult;
                    }

                case JsonToken.Date:
                    {
                        var dateResult = Convert.ToDateTime(reader.Value);
                        Log.D($"转换日期: {dateResult}");
                        return dateResult;
                    }

                case JsonToken.Null:
                    return default!;

                default:
                    Log.D($"未处理的类型: {reader.TokenType}, 值: {reader.Value}");
                    return reader.Value?.ToString() ?? string.Empty;
            }
        }

        /// <summary>
        /// 读取JSON对象并转换为Dictionary
        /// </summary>
        private Dictionary<string, object> ReadObject(JsonReader reader)
        {
            var dict = new Dictionary<string, object>(StringComparer.Ordinal);

            while (reader.Read() && reader.TokenType != JsonToken.EndObject)
            {
                if (reader.TokenType != JsonToken.PropertyName)
                    continue;

                string propertyName = reader.Value != null ? reader.Value.ToString() : string.Empty;

                // 读取属性值
                reader.Read();
                object? value = null;
                try
                {
                    value = ReadValue(reader);
                }
                catch (Exception ex)
                {
                    Log.W($"读取属性值异常: {propertyName}, {ex.Message}");
                    value = string.Empty;
                }

                dict[propertyName] = value ?? string.Empty;
                Log.D($"读取对象属性: {propertyName}={value?.GetType().Name ?? "null"}");
            }

            Log.D($"对象解析完成: {dict.Count}个属性");
            return dict;
        }

        /// <summary>
        /// 读取JSON数组并转换为List
        /// </summary>
        private List<object> ReadArray(JsonReader reader)
        {
            var list = new List<object>();

            while (reader.Read() && reader.TokenType != JsonToken.EndArray)
            {
                object? value = null;
                try
                {
                    value = ReadValue(reader);
                }
                catch (Exception ex)
                {
                    Log.W($"读取数组项异常: {ex.Message}");
                    value = string.Empty;
                }
                list.Add(value ?? string.Empty);
                Log.D($"读取数组项: {value?.GetType().Name ?? "null"}");
            }

            Log.D($"数组解析完成: {list.Count}个项");
            return list;
        }
    }

    [JsonConverter(typeof(IOSSqliteResultConverter))]
    internal class IOSSqliteResult
    {
        /// <summary>
        /// 调用ID，用于匹配请求和响应
        /// </summary>
        public int CallId { get; set; } = -1;

        /// <summary>
        /// 数据对象，可以是任何类型（Int, float, bool, Dictionary, List等）
        /// </summary>
        public object Data { get; set; }

        public IOSSqliteResult(int callId = -1, object? data = null)
        {
            CallId = callId;
            Data = data ?? new Dictionary<string, object>(StringComparer.Ordinal);
        }

        /// <summary>
        /// 从JSON字符串反序列化IOSSqliteResult对象
        /// </summary>
        /// <param name="json">JSON字符串</param>
        /// <returns>解析后的IOSSqliteResult对象，解析失败则返回null</returns>
        public static IOSSqliteResult? FromJson(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                Log.W("FromJson: 收到的JSON字符串为空");
                return null;
            }

            try
            {
                Log.D($"解析JSON: {json}");
                return JsonConvert.DeserializeObject<IOSSqliteResult>(json);
            }
            catch (Exception ex)
            {
                Log.E($"从JSON解析IOSSqliteResult失败: {ex.Message}");
                return null;
            }
        }
    }

    public class IOSSqliteApi : IGuruSqliteApi
    {
        private static int _callIdPools = 0;

        private static int BuildCallId()
        {
            return Interlocked.Increment(ref _callIdPools);
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void MethodResultCallback(IntPtr result);

        // 声明与iOS原生层通信的外部方法
        [DllImport("__Internal")]
        private static extern void InvokeMethod(int callId, string method, string arguments, MethodResultCallback onMethodResultCallback);

        private static readonly Dictionary<int, MethodRequest> _methodRequests = new();
        private static readonly object _lock = new object();

        /// <summary>
        /// 调用iOS原生层方法并异步获取结果
        /// </summary>
        /// <typeparam name="T">返回的数据类型</typeparam>
        /// <param name="method">调用的方法名</param>
        /// <param name="arguments">方法参数</param>
        /// <returns>包含类型T的异步任务</returns>
        public UniTask<T> InvokeMethod<T>(string method, object arguments)
        {
            try
            {
                var callId = BuildCallId();
                string args = JsonConvert.SerializeObject(arguments);
                var methodCall = new MethodCall(method, arguments);

                MethodRequest? request;
                lock (_lock)
                {
                    request = new MethodRequest(callId, methodCall);
                    _methodRequests[callId] = request;
                }

                InvokeMethod(callId, methodCall.Method, args, OnMethodResult);
                return request.MethodResult.Tcs.Task.ContinueWith((result) =>
                {
                    try
                    {
                        if (result == null)
                        {
                            if (typeof(T).IsValueType)
                            {
                                throw new InvalidCastException($"无法将null转换为值类型 {typeof(T).Name}");
                            }
                            // For reference types, return default (which is null)
                            return default(T)!;
                        }
                        return (T)result;
                    }
                    catch (InvalidCastException ex)
                    {
                        Log.E($"无法将结果转换为类型 {typeof(T).Name}: {ex.Message}");
                        throw new InvalidCastException($"无法将结果转换为类型 {typeof(T).Name}: {method}", ex);
                    }
                });
            }
            catch (Exception ex)
            {
                Log.E($"调用方法 {method} 时出错: {ex.Message}");
                return UniTask.FromException<T>(ex);
            }
        }

        /// <summary>
        /// 处理原生层返回的方法调用结果
        /// </summary>
        /// <param name="resultPtr">指向返回数据的指针</param>
        [MonoPInvokeCallback(typeof(MethodResultCallback))]
        public static void OnMethodResult(IntPtr resultPtr)
        {
            // 检查指针是否为空
            if (resultPtr == IntPtr.Zero)
            {
                throw new ArgumentNullException(nameof(resultPtr), "收到空指针");
            }

            string? resultStr = null;
            IOSSqliteResult? result = null;
            int? callId = null;
            MethodRequest? pendingRequest = null;

            // 转换指针到字符串
            resultStr = Marshal.PtrToStringAnsi(resultPtr);
            Log.D("OnMethodResult: " + resultStr);

            if (string.IsNullOrEmpty(resultStr))
            {
                throw new ArgumentNullException(nameof(resultStr), "收到空结果字符串");
            }

            // 解析JSON
            result = IOSSqliteResult.FromJson(resultStr);
            if (result == null)
            {
                throw new JsonException("无法解析结果JSON");
            }

            callId = result.CallId;
            if (callId <= 0)
            {
                throw new ArgumentException($"CallId无效: {callId}");
            }

            // 查找并处理请求
            lock (_lock)
            {
                if (_methodRequests.TryGetValue(callId.Value, out var request))
                {
                    pendingRequest = request;
                    _methodRequests.Remove(callId.Value);
                }
                else
                {
                    var errorMessage = $"未找到callId {callId}的待处理请求";
                    Log.E(errorMessage);
                    throw new KeyNotFoundException(errorMessage);
                }
            }

            pendingRequest?.MethodResult.OnResult(result.Data);
        }
    }
}
#endif