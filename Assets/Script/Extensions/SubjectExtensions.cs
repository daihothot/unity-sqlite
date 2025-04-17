using R3;

namespace Guru.SDK.Framework.Utils.Extensions
{
    public static class SubjectExtensions
    {
        public static void AddEx<T>(this BehaviorSubject<T> subject, T value)
        {
            if (!subject.IsDisposed)
            {
                subject.OnNext(value);
            }
        }
        
        public static void AddEx<T>(this Subject<T> subject, T value)
        {
            if (!subject.IsDisposed)
            {
                subject.OnNext(value);
            }
        }

        public static void AddIfChanged<T>(this BehaviorSubject<T> subject, T value)
        {
            if (!subject.IsDisposed && !Equals(subject.Value, value))
            {
                subject.OnNext(value);
            }
        }
    }
}