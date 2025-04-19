#nullable enable
using System;
using System.Collections.Concurrent;
using System.Threading;
using Guru.SDK.Framework.Utils.DateTime;
using Guru.SDK.Framework.Utils.Log;
using UnityEngine;

namespace GuruSqlite
{
    public class AndroidSqliteTransport
    {

        private volatile bool running; 
        private readonly BlockingCollection<Action> _taskQueue = new();
        private readonly CancellationTokenSource cts = new();

        internal AndroidSqliteTransport(string name)
        {
            new Thread(Looper)
            {
                Name = name,
                IsBackground = true
            }.Start();
        }

        internal void Close()
        {
            running = false;
            cts.Cancel();
        }
        
        private void Looper()
        {
            AndroidJNI.AttachCurrentThread();
            Log.D($"AndroidSqliteTransport Thread STARTED. [{Thread.CurrentThread.ManagedThreadId}] {Thread.CurrentThread.Name}");
            running = true;
            try
            {
                while (running && _taskQueue.TryTake(out var action, (int)DateTimeUtils.FiveMinutesInMillis, cts.Token))
                {
                    Log.D("Database thread task received.");
                    action.Invoke();
                }
            }
            catch (Exception exception)
            {
                Log.W("AndroidSqliteTransport thread exception: " + exception);
            }
            finally
            {
                Log.W("AndroidSqliteTransport Detached");
                AndroidJNI.DetachCurrentThread();
            }

        }

        internal void Dispatch(Action action)
        {
            _taskQueue.Add(action);
        }

    }
}