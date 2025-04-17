using System;
using Cysharp.Threading.Tasks;

namespace Guru.SDK.Framework.Utils.Extensions
{
    public static class UniTaskExtensions
    {
        public static async UniTask<T> CatchError<T>(this UniTask<T> task, Func<Exception, T> onError)
        {
            try
            {
                return await task;
            }
            catch (Exception ex)
            {
                return onError(ex);
            }
        }

        public static async UniTask CatchError(this UniTask task, Action<Exception> onError)
        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex);
                throw;
            }
        }
        
    }
}