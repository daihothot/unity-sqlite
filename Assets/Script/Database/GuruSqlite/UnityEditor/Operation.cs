#nullable enable
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine.Device;

namespace GuruSqlite
{
    public interface IOperation : IOperationResult
    {
        string GetMethod();
        T GetArgument<T>(string key);
        bool HasArgument(string key);
        SqlCommand GetSqlCommand();
        bool GetNoResult();
        bool GetContinueOnError();
        bool? GetInTransactionChange();
        int? GetTransactionId();
        bool HasNullTransactionId();
    }

    public interface IOperationResult
    {
        void Success(object result);
        void Error(string errorCode, string errorMessage, object? data);
    }

    public abstract class BaseReadOperation : IOperation
    {
        protected abstract IOperationResult GetOperationResult();

        public abstract string GetMethod();
        public abstract T GetArgument<T>(string key);
        public abstract bool HasArgument(string key);
        // public abstract bool GetNoResult();
        // public abstract bool GetContinueOnError();
        
        public bool GetNoResult()
        {
            var value = GetArgument<bool>(GuruSqliteConstants.ParamContinueOnError);
            return value;
        }

        public bool GetContinueOnError()
        {
            var value = GetArgument<bool>(GuruSqliteConstants.ParamContinueOnError);
            return value;
        }

        public virtual string GetSql() => GetArgument<string>(GuruSqliteConstants.ParamSql);
        public virtual object[] GetSqlArguments() => GetArgument<object[]?>(GuruSqliteConstants.ParamSqlArguments) ?? Array.Empty<object>();
        public virtual int? GetTransactionId() => GetArgument<int?>("transaction_id");
        public virtual bool HasNullTransactionId() => HasArgument("transaction_id") && GetTransactionId() == null;

        public virtual SqlCommand GetSqlCommand() =>
            new SqlCommand(SqlCommandType.Execute, GetSql(), GetSqlArguments());

        public virtual bool? GetInTransactionChange() => GetBoolean("in_transaction_change");

        private bool? GetBoolean(string key)
        {
            var value = GetArgument<object>(key);
            return value is bool b ? b : null;
        }

        public void Success(object result) => GetOperationResult().Success(result);

        public void Error(string errorCode, string errorMessage, object? data) =>
            GetOperationResult().Error(errorCode, errorMessage, data);

        public override string ToString() => $"{GetMethod()} {GetSql()} [{string.Join(", ", GetSqlArguments())}]";
    }

    public abstract class BaseOperation : BaseReadOperation
    {
        protected abstract override IOperationResult GetOperationResult();
    }

    public class MethodCallOperation : BaseOperation
    {
        public Result ResultWrapper { get; }
        private readonly MethodCall _methodCall;

        public MethodCallOperation(MethodCall methodCall, SqliteResult sqliteResult)
        {
            _methodCall = methodCall;
            ResultWrapper = new Result(sqliteResult);
        }

        public override string GetMethod() => _methodCall.Method;
        public override T GetArgument<T>(string key) => _methodCall.GetArgument<T>(key);
        public override bool HasArgument(string key) => _methodCall.HasArgument(key);

        protected override IOperationResult GetOperationResult() => ResultWrapper;

        public class Result : IOperationResult
        {
            private readonly SqliteResult _result;
            public Result(SqliteResult result) => _result = result;
            public void Success(object result) => _result.Success(result);

            public void Error(string errorCode, string errorMessage, object? data) =>
                _result.Error(errorCode, errorMessage, data);
        }
    }

    public class EditorBatchOperation : BaseOperation
    {
        private readonly Dictionary<string, object> _map;
        private readonly bool _noResult;
        private readonly BatchOperationResult _operationResult = new();

        public EditorBatchOperation(Dictionary<string, object> map, bool noResult)
        {
            _map = map;
            _noResult = noResult;
        }

        public override string GetMethod() => (string)_map["method"];
        public override T GetArgument<T>(string key) => _map.TryGetValue(key, out var value) ? (T)value : default!;
        public override bool HasArgument(string key) => _map.ContainsKey(key);

        protected override IOperationResult GetOperationResult() => _operationResult;

        public Dictionary<string, object> GetOperationSuccessResult() => new()
        {
            ["result"] = _operationResult.Result
        };

        public Dictionary<string, object> GetOperationError() => new()
        {
            ["error"] = new Dictionary<string, object>
            {
                ["code"] = _operationResult.ErrorCode,
                ["message"] = _operationResult.ErrorMessage,
                ["data"] = _operationResult.ErrorData
            }
        };

        public void HandleError(SqliteResult result)
        {
            result.Error(_operationResult.ErrorCode, _operationResult.ErrorMessage, _operationResult.ErrorData);
        }

        public void HandleSuccess(List<Dictionary<string, object>> results)
        {
            if (!GetNoResult())
            {
                results.Add(GetOperationSuccessResult());
            }
        }

        public void HandleErrorContinue(List<Dictionary<string, object>> results)
        {
            if (!GetNoResult())
            {
                results.Add(GetOperationError());
            }
        }

        private class BatchOperationResult : IOperationResult
        {
            public object? Result { get; private set; }
            public string? ErrorCode { get; private set; }
            public string? ErrorMessage { get; private set; }
            public object? ErrorData { get; private set; }

            public void Success(object result) => Result = result;

            public void Error(string errorCode, string errorMessage, object? data)
            {
                ErrorCode = errorCode;
                ErrorMessage = errorMessage;
                ErrorData = data;
            }
        }
    }
}
#endif