#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Guru.SDK.Framework.Utils.Log;
using Newtonsoft.Json;

namespace GuruSqlite
{
    public abstract class SqliteResult
    {
        public abstract void Success(object? result);

        public abstract void Error(string code, string message, object? details);

        public abstract void NotImplemented();
    }

    public class EditorSqliteResult<T> : SqliteResult
    {
        internal readonly UniTaskCompletionSource<T> Tcs = new();

        public EditorSqliteResult()
        {
        }

        public override void Success(object? result)
        {
            Log.I(
                $"OnResult: ExpectType: {typeof(T)} ResultType:{result?.GetType()} Result:{JsonConvert.SerializeObject(result)}");

            switch (result)
            {
                case null:
                    Tcs.TrySetResult(default!);
                    return;
                case T typedResult:
                    Tcs.TrySetResult(typedResult);
                    break;
                default:
                    Log.W(
                        $"SqliteResultCallback OnResult: Type mismatch. ResultType:{result?.GetType()} ExpectType: {typeof(T)}");
                    Tcs.TrySetException(new Exception($"Format error: {result} ExpectType: {typeof(T)}"));
                    break;
            }
        }

        public override void Error(string code, string message, object? details)
        {
            Log.W($"OnError: {code} {message} {details}");

            Tcs.TrySetException(new Exception($"{code}: {message}"));
        }

        public override void NotImplemented()
        {
            Log.W($"OnNotImplemented");
            Tcs.TrySetException(new NotImplementedException());
        }


        public void SqliteQuerySuccess(List<Dictionary<string, object>> rows)
        {
            var result = new Dictionary<string, object>
            {
                { "rows", rows },
                { "rowCount", rows.Count }
            };
            Success(result);
        }

        public void SqliteExecuteSuccess(int rowsAffected, long? insertId)
        {
            var result = new Dictionary<string, object>
            {
                { "rowsAffected", rowsAffected }
            };

            if (insertId.HasValue)
            {
                result["insertId"] = insertId.Value;
            }

            Success(result);
        }

        public void SqliteError(string code, string message)
        {
            Error("sqlite_error", message, code);
        }

        public void SqliteTransactionResult(bool successful)
        {
            var result = new Dictionary<string, object>
            {
                { "transactionSuccessful", successful }
            };
            Success(result);
        }
    }
}
#endif