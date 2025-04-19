#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Guru.SDK.Framework.Utils.Log;
using Newtonsoft.Json;
using UnityEngine;

namespace GuruSqlite
{
    // Android平台下与Java交互的辅助类
    public class AndroidSqliteApi : IGuruSqliteApi
    {

        private const string Tag = "GuruSqlite";
        private readonly AndroidJavaObject _plugin;

        private readonly AndroidSqliteTransport _invoker;


        // 在应用启动时初始化Java插件
        internal AndroidSqliteApi()
        {
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            _invoker = new AndroidSqliteTransport("NativeInvoker");
            _plugin = new AndroidJavaObject("guru.core.sqlite.SqlitePlugin", activity);
            // _plugin.Call("bindInvoker", _invoker);

        }

        
        // 用于调用Java的invoke方法
        private void Invoke(MethodCall call, IResultCallback callback)
        {
            // Log.I($"Invoke: {call.Method}", "GuruSqlite");
            // 创建Java端需要的MethodCall对象
            
            using var javaMethodCall = CreateJavaMethodCall(call);
            // 创建Java端需要的SqliteResult对象
            using var javaSqliteResult = CreateJavaSqliteResult(call.Method, callback);
            // 调用Java的invoke方法
            Log.I($"invoke call: {call.Method} {call.Arguments}", "GuruSqlite");

            try
            {
                _plugin.Call("invoke", javaMethodCall, javaSqliteResult);
            }
            catch (Exception exception)
            {
                Log.W($"invoke error! {exception.Message}");
            }

            Log.I($"invoke completed", "GuruSqlite");
        }

        public UniTask<T?> InvokeMethod<T>(string method, object arguments)
        {
            var call = new MethodCall(method, arguments);
            var callback = new SqliteResultCallback<T?>();
            _invoker.Dispatch(() => Invoke(call, callback));
            return callback.Tcs.Task;
        }

        // // 创建Java端的MethodCall对象
        private AndroidJavaObject CreateJavaMethodCall(MethodCall call)
        {
            var arguments = JsonConvert.SerializeObject(call.Arguments);
            Log.I($"CreateJavaMethodCall: {call.Method} {arguments}", "GuruSqlite");
            var javaObject = call.Arguments switch
            {
                Dictionary<string, object> dict => ConvertDictionaryToJavaMap(dict),
                IList<object> list => ConvertListToJavaList(list),
                _ => call.Arguments
            };
            
            Log.I($"javaObject.Type: {javaObject.GetType()}", "GuruSqlite");
            
            
            // 创建Java端的MethodCall对象，传入method和arguments
            return new AndroidJavaObject("guru.core.sqlite.MethodCall", call.Method, javaObject);
        }

        // 将C#的Dictionary转换为Java的HashMap
        private AndroidJavaObject ConvertDictionaryToJavaMap(object obj)
        {
            // Log.I($"ConvertDictionaryToJavaMap {dict.Count}", "GuruSqlite");
            var javaMap = new AndroidJavaObject("java.util.HashMap");
            
            if (obj is not Dictionary<string, object> dict)
            {
                return javaMap;
            }
        
            foreach (var pair in dict)
            {
                if (pair.Value == null)
                {
                    continue;
                }
                var javaKey = new AndroidJavaObject("java.lang.String", pair.Key);
                
                AndroidJavaObject? javaValue = null;
                if (IsGenericDictionary(pair.Value))
                {
                    javaValue = ConvertDictionaryToJavaMap(pair.Value);
                } else if (IsGenericList(pair.Value))
                {
                    javaValue = ConvertListToJavaList(pair.Value);
                }

                javaValue ??= pair.Value switch
                {
                    // 根据值的类型进行转换
                    int i => new AndroidJavaObject("java.lang.Integer", i),
                    bool b => new AndroidJavaObject("java.lang.Boolean", b),
                    float f => new AndroidJavaObject("java.lang.Float", f),
                    double d => new AndroidJavaObject("java.lang.Double", d),
                    long l => new AndroidJavaObject("java.lang.Long", l),
                    _ => new AndroidJavaObject("java.lang.String",  pair.Value.ToString())
                };
                // Log.I($"  ==> {pair.Key}:{pair.Value}", "GuruSqlite");
                var args = new object[2];
                javaMap.Call<AndroidJavaObject>("put", javaKey, javaValue);
                // AndroidJNI.CallObjectMethod(javaMap.GetRawObject(),
                    // putMethod, AndroidJNIHelper.CreateJNIArgArray(args));
            }
            
            return javaMap;
        }

        private bool IsGenericList(object? obj)
        {
            return obj is IList;
        }
        
        private bool IsGenericDictionary(object? obj)
        {
            return obj is IDictionary;
        }
        
        // 将C#的List转换为Java的ArrayList
        private AndroidJavaObject ConvertListToJavaList(object obj)
        {
            var javaList = new AndroidJavaObject("java.util.ArrayList");

            if (obj is not IEnumerable list)
            {
                return javaList;
            }

            foreach (var item in list)
            {

                AndroidJavaObject? value = null;
                if (IsGenericDictionary(item))
                {
                    value = ConvertDictionaryToJavaMap(item);
                } else if (IsGenericList(item))
                {
                    value = ConvertListToJavaList(item);
                }

                value ??= item switch
                {
                    int i => new AndroidJavaObject("java.lang.Integer", i),
                    bool b => new AndroidJavaObject("java.lang.Boolean", b),
                    float f => new AndroidJavaObject("java.lang.Float", f),
                    double d => new AndroidJavaObject("java.lang.Double", d),
                    long l => new AndroidJavaObject("java.lang.Long", l),
                    _ => new AndroidJavaObject("java.lang.String", item.ToString())
                };

                javaList.Call<bool>("add", value);
            }
        
            return javaList;
        }

        // 创建Java端的SqliteResult对象
        private AndroidJavaObject CreateJavaSqliteResult(string method, IResultCallback callback)
        {
            AndroidJavaProxy javaCallback = new JavaResultCallbackProxy(method, callback);

            return new AndroidJavaObject("guru.core.sqlite.SqliteResult", javaCallback);
        }

        // Java回调接口的代理实现
        private class JavaResultCallbackProxy : AndroidJavaProxy
        {
            private readonly string _method;
            private readonly IResultCallback _callback;

            public JavaResultCallbackProxy(string method, IResultCallback callback) : base(
                "guru.core.sqlite.SqliteResult$ResultCallback")
            {
                _method = method;
                _callback = callback;
            }

            // 实现Java接口中的方法
            public void onResult(object result)
            {
                // 将Java对象转换为C#对象
                var convertedResult = ConvertJavaObjectToCSharp(result);
                
                Log.I($"[{_method}] Invoke Completed!", "GuruSqlite");

                    // 调用C#回调
                _callback.OnResult(convertedResult);
            }

            public void onError(string code, string message, object details)
            {
                Log.I($"[{_method}] onError {code} {message}", "GuruSqlite");
                // 将Java对象转换为C#对象
                var convertedDetails = ConvertJavaObjectToCSharp(details);
                
                    // 调用C#回调
                _callback.OnError(code, message, convertedDetails);
            }

            public void onNotImplemented()
            {
                Log.I($"[{_method}] onNotImplemented", "GuruSqlite");
                _callback.OnNotImplemented();
            }


            // 将Java对象转换为C#对象
            private object? ConvertJavaObjectToCSharp(object? javaObject)
            {
                if (javaObject == null)
                {
                    return null;
                }

                if (javaObject is not AndroidJavaObject ajo)
                {
                    // 如果不是AndroidJavaObject，则直接返回原始对象
                    return javaObject;
                }

                using var javaClass = new AndroidJavaClass("java.lang.Class");
                using var objectClass = ajo.Call<AndroidJavaObject>("getClass");
                var className = objectClass.Call<string>("getName");
                Log.D($"ConvertJavaObjectToCSharp: {className}", tag: Tag);
                return className switch
                {
                    // 根据Java类名进行相应的转换
                    "java.util.HashMap" or "java.util.Map" => ConvertJavaMapToDictionary(ajo),
                    "java.util.ArrayList" or "java.util.List" or "java.util.Arrays$ArrayList" => ConvertJavaListToList(ajo),
                    "java.lang.Integer" => ajo.Call<int>("intValue"),
                    "java.lang.Boolean" => ajo.Call<bool>("booleanValue"),
                    "java.lang.Float" => ajo.Call<float>("floatValue"),
                    "java.lang.Double" => ajo.Call<double>("doubleValue"),
                    "java.lang.Long" => ajo.Call<long>("longValue"),
                    _ => ajo.Call<string>("toString")
                };
            }

            // 将Java的Map转换为C#的Dictionary
            private Dictionary<string, object> ConvertJavaMapToDictionary(AndroidJavaObject javaMap)
            {
                var result = new Dictionary<string, object>();

                using var entrySet = javaMap.Call<AndroidJavaObject>("entrySet");
                using var iterator = entrySet.Call<AndroidJavaObject>("iterator");
                while (iterator.Call<bool>("hasNext"))
                {
                    using var entry = iterator.Call<AndroidJavaObject>("next");
                    var key = entry.Call<AndroidJavaObject>("getKey").Call<string>("toString");
                    var value = entry.Call<AndroidJavaObject>("getValue");
                    var validValue = ConvertJavaObjectToCSharp(value);
                    if (validValue == null) continue;
                    result[key] = validValue;
                    Log.I($" ==> [{key}] = {validValue}", Tag);
                }

                return result;
            }

            // 将Java的List转换为C#的List
            private List<object> ConvertJavaListToList(AndroidJavaObject javaList)
            {
                Log.I($"ConvertJavaListToList ", Tag);
                var result = new List<object>();

                var size = javaList.Call<int>("size");
                for (var i = 0; i < size; i++)
                {
                    var item = javaList.Call<AndroidJavaObject>("get", i);
                    var value = ConvertJavaObjectToCSharp(item);
                    Log.I($" ==> {value}", Tag);
                    if (value != null)
                    {
                        result.Add(value);
                    }
                }

                return result;
            }
        }
    }
}