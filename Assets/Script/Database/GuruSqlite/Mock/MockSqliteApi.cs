using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Guru.SDK.Framework.Utils.Log;
using Newtonsoft.Json;
using UnityEngine.Windows;

namespace GuruSqlite
{
#if UNITY_EDITOR
    public class MockSqliteApi : IGuruSqliteApi
    {
        // 在应用启动时初始化Java插件
        internal MockSqliteApi()
        {
        }

        public UniTask<T> InvokeMethod<T>(string method, object arguments)
        {
            var argumentsString = JsonConvert.SerializeObject(arguments);
            Log.I($"method:{method} {argumentsString}");
            switch (method)
            {
                case "openDatabase":
                    var result = new Dictionary<string, object>
                    {
                        { "id", 1 }
                    };
                    
                    return UniTask.FromResult((T)(object)result);
            }
            return UniTask.FromResult(default(T));
        }
    }
#endif
}