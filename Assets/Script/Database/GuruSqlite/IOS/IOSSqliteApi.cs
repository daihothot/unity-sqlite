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
            var resultStr = Marshal.PtrToStringAnsi(resultPtr);
            var methodRequest = JsonConvert.DeserializeObject<MethodRequest>(resultStr ?? "");
            var callId = methodRequest?.CallId;
            if (callId == null)
            {
                Log.W("OnMethodResult: callId is null");
                return;
            }

            var id = callId ?? 0;

            MethodRequest? pendingRequest = null;
            lock (_lock)
            {
                if (_methodRequests.Remove(id, out var request))
                {
                    pendingRequest = request;
                }
            }

            pendingRequest?.MethodResult.OnResult(resultStr);

        }


    }
    


}
#endif