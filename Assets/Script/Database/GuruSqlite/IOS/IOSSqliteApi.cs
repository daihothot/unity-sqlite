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

    internal class IOSSqliteResult
    {
        [JsonProperty("id", DefaultValueHandling = DefaultValueHandling.Populate)]
        public int CallId { get; set; } = -1;

        [JsonProperty("args", DefaultValueHandling = DefaultValueHandling.Populate)]
        public object Arguments { get; set; }

        public IOSSqliteResult(int callId = -1, object? arguments = null)
        {
            CallId = callId;
            Arguments = arguments ?? new Dictionary<string, object>();
        }

        public static IOSSqliteResult? FromJson(string json)
        {
            return JsonConvert.DeserializeObject<IOSSqliteResult>(json);
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

        // 声明外部方法
        [DllImport("__Internal")]
        private static extern void InvokeMethod(int callId, string method, string arguments, MethodResultCallback onMethodResultCallback);

        private static readonly Dictionary<int, MethodRequest> _methodRequests = new();
        private static readonly object _lock = new object();

        public UniTask<T> InvokeMethod<T>(string method, object arguments)
        {
            var callId = BuildCallId();
            var args = JsonConvert.SerializeObject(arguments);
            var methodCall = new MethodCall(method, arguments);
            InvokeMethod(callId, methodCall.Method, args, OnMethodResult);
            MethodRequest? request;
            lock (_lock)
            {
                request = new MethodRequest(callId, methodCall);
                _methodRequests[callId] = request;
            }

            return request.MethodResult.Tcs.Task.ContinueWith((result) => (T)(object)result);
        }


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
                if (_methodRequests.Remove(callId.Value, out var request))
                {
                    pendingRequest = request;
                }
                else
                {
                    Log.W($"OnMethodResult: 未找到callId {callId}的待处理请求");
                }
            }

            pendingRequest?.MethodResult.OnResult(result.Arguments);
        }
    }
}
#endif