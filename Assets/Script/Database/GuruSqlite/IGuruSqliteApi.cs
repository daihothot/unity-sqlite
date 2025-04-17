using Cysharp.Threading.Tasks;

namespace GuruSqlite
{
    public interface IGuruSqliteApi
    {
        public UniTask<T> InvokeMethod<T>(string method, object arguments);
    }
}