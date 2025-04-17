#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Guru.SDK.Framework.Utils.Timer
{
    using Log;

    public delegate void TimerElapsedHandler();

    public class UniTimer : IDisposable
    {
        private CancellationTokenSource _cts;
        private readonly TimeSpan _duration;
        private readonly bool _isPeriodic;
        private readonly TimerElapsedHandler? _elapsed;
        private bool _isDispose;

        public bool IsActive => !_isDispose && !_cts.Token.IsCancellationRequested;


        private UniTimer(TimeSpan duration, bool isPeriodic, TimerElapsedHandler elapsed)
        {
            _duration = duration;
            _cts = new CancellationTokenSource();
            _isPeriodic = isPeriodic;
            _elapsed = elapsed;
        }

        public static UniTimer Delayed(TimeSpan duration, TimerElapsedHandler elapsed)
        {
            return new UniTimer(duration, false, elapsed);
        }


        public static UniTimer Periodic(TimeSpan interval, TimerElapsedHandler elapsed)
        {
            return new UniTimer(interval, true, elapsed);
        }

        private async UniTaskVoid StartInternal(CancellationToken token)
        {
            try
            {
                do
                {
                    Log.D("Timer tick at: " + System.DateTime.UtcNow);

                    await UniTask.Delay(Convert.ToInt32(_duration.TotalMilliseconds), cancellationToken: token);

                    if (token.IsCancellationRequested)
                    {
                        Log.D("Timer cancelled!");
                        break;
                    }

                    _elapsed?.Invoke();
                } while (_isPeriodic && !token.IsCancellationRequested);
            }
            catch (OperationCanceledException)
            {
                Log.D("Timer cancelled (OperationCanceledException)!");
            }
        }

        public void Start()
        {
            if (_isDispose)
            {
                Log.D("Timer disposed.");
                return;
            }

            if (IsActive)
            {
                Log.W("Timer already started.");
                return;
            }

            _cts.Dispose();
            _cts = new CancellationTokenSource();
            StartInternal(_cts.Token).Forget();
            Log.D("Timer started.");
        }

        public void Stop()
        {
            if (_cts.IsCancellationRequested) return;
            _cts.Cancel();
        }

        public void Dispose()
        {
            Stop();
            _isDispose = true;
        }
    }
}