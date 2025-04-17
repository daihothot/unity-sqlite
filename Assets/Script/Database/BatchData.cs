#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Guru.SDK.Framework.Utils.Extensions;
using R3;

namespace Guru.SDK.Framework.Utils.Database
{
    public enum BatchMethod
    {
        Insert,
        Update,
        Delete,
        Select,
        Clear,
        Remove,
        Replace,
        Error
    }

    public enum BatchState
    {
        Ignore,
        Success,
        Error
    }

    public static class BatchMethods
    {
        public static readonly List<BatchMethod> AllMethods = new()
        {
            BatchMethod.Insert,
            BatchMethod.Update,
            BatchMethod.Delete,
            BatchMethod.Select,
            BatchMethod.Clear,
            BatchMethod.Remove,
            BatchMethod.Replace,
            BatchMethod.Error
        };
    }

    public class BatchResult
    {
        public BatchState State { get; }
        public object? Cause { get; }

        public static readonly BatchResult Success = new(BatchState.Success);

        public BatchResult(BatchState state, object? cause = null)
        {
            State = state;
            Cause = cause;
        }
    }

    public class BatchAction<T>
    {
        public BatchMethod Method { get; }
        public readonly List<T> Items;
        public readonly BatchResult Result;
        public readonly Func<T, bool>? Where;
        public readonly Func<T, T>? Updater;
        public int Length => Items.Count;

        public BatchAction(BatchMethod method, List<T>? items = null, BatchResult? result = null,
            Func<T, bool>? where = null, Func<T, T>? updater = null)
        {
            Method = method;
            Items = items ?? new();
            Result = result ?? BatchResult.Success;
            Where = where;
            Updater = updater;
        }

        public void Append(T? data)
        {
            if (data != null) Items.Add(data);
        }

        public void AppendAll(IEnumerable<T> data)
        {
            Items.AddRange(data);
        }
    }

    public class BatchData<T>
    {
        private readonly List<BatchAction<T>> _actions = new();
        public IReadOnlyList<BatchAction<T>> Actions => _actions;


        private BatchData()
        {
        }

        private BatchData(BatchMethod method, List<T> data, BatchResult? result = null)
        {
            AppendAll(method, data, result ?? BatchResult.Success);
        }

        public static BatchData<T> SingleSuccess(BatchMethod method, T data)
        {
            return new BatchData<T>(method, new List<T> { data }, BatchResult.Success);
        }

        public static BatchData<T> SingleSuccess(BatchMethod method)
        {
            return new BatchData<T>(method, new List<T>(), BatchResult.Success);
        }

        public static BatchData<T> SingleError(BatchMethod method, T data, BatchResult? result = null)
        {
            return new BatchData<T>(method, new List<T> { data }, result);
        }

        public static BatchData<T> SingleError(BatchMethod method, BatchResult? result = null)
        {
            return new BatchData<T>(method, new List<T>(), result ?? new BatchResult(BatchState.Error));
        }

        public static BatchData<T> Empty()
        {
            return new BatchData<T>();
        }

        private void Append(BatchMethod method, T data, BatchResult? result = null)
        {
            var batchAction = _actions.LastOrDefault();
            if (batchAction != null && batchAction.Method == method && batchAction.Result.State == result?.State)
            {
                batchAction.Append(data);
            }
            else
            {
                _actions.Add(new BatchAction<T>(method, new List<T> { data }, result));
            }
        }

        private void AppendAll(BatchMethod method, List<T> data, BatchResult? result = null)
        {
            var batchAction = _actions.LastOrDefault();
            if (batchAction != null && batchAction.Method == method && batchAction.Result.State == result?.State)
            {
                batchAction.AppendAll(data);
            }
            else
            {
                _actions.Add(new BatchAction<T>(method, data, result));
            }
        }


        public void Insert(T data, BatchResult? result = null)
        {
            Append(BatchMethod.Insert, data, result);
        }

        public void InsertAll(List<T> data, BatchResult? result = null)
        {
            AppendAll(BatchMethod.Insert, data, result);
        }

        public void Update(T data, BatchResult? result = null)
        {
            Append(BatchMethod.Update, data, result);
        }

        public void UpdateAll(List<T> data, BatchResult? result = null)
        {
            AppendAll(BatchMethod.Update, data, result);
        }

        public void Replace(Func<T, bool>? where = null, Func<T, T>? updater = null, BatchResult? result = null)
        {
            _actions.Add(new BatchAction<T>(BatchMethod.Replace, result: result ?? BatchResult.Success,
                where: where ?? (_ => true), updater: updater ?? (r => r)));
        }

        public void Delete(T data, BatchResult? result = null)
        {
            Append(BatchMethod.Delete, data, result);
        }

        public void DeleteAll(List<T> data, BatchResult? result = null)
        {
            AppendAll(BatchMethod.Delete, data, result);
        }

        
        public void Query(T data, BatchResult? result = null)
        {
            Append(BatchMethod.Select, data, result);
        }

        public void QueryAll(List<T> data, BatchResult? result = null)
        {
            AppendAll(BatchMethod.Select, data, result);
        }

        public void Clear()
        {
            Append(BatchMethod.Clear, default!);
        }

        public void RemoveWhere(Func<T, bool>? where = null)
        {
            _actions.Add(new BatchAction<T>(BatchMethod.Remove, result: BatchResult.Success,
                where: where ?? (_ => true)));
        }

        public bool ContainsMethodResult(BatchMethod method, List<BatchState> states)
        {
            return _actions.Any(action => action.Method == method && states.Contains(action.Result.State));
        }

        public bool IsEmpty => !_actions.Any();
        public bool IsNotEmpty => _actions.Any();
        public int Length => _actions.Sum(action => action.Length);

        public bool HasError => ContainsMethodResult(BatchMethod.Error, new List<BatchState> { BatchState.Error });

        public bool HasInsertSuccess =>
            ContainsMethodResult(BatchMethod.Insert, new List<BatchState> { BatchState.Success });

        public bool HasUpdateSuccess =>
            ContainsMethodResult(BatchMethod.Update, new List<BatchState> { BatchState.Success });

        public bool HasDeleteSuccess =>
            ContainsMethodResult(BatchMethod.Delete, new List<BatchState> { BatchState.Success });

        public int Size(List<BatchMethod> methods, BatchResult? result = null)
        {
            return IsEmpty
                ? 0
                : _actions
                    .Where(action =>
                        methods.Contains(action.Method) && (result == null || action.Result.State == result.State))
                    .Sum(action => action.Length);
        }

        public List<BatchAction<T>> GetActions(List<BatchMethod>? methods = null, BatchResult? result = null)
        {
            methods ??= BatchMethods.AllMethods;
            return IsEmpty
                ? new List<BatchAction<T>>()
                : _actions
                    .Where(action =>
                        methods.Contains(action.Method) && (result == null || action.Result.State == result.State))
                    .ToList();
        }

        public T? First(List<BatchMethod>? methods = null, BatchResult? result = null)
        {
            methods ??= BatchMethods.AllMethods;
            foreach (var action in _actions
                         .Where(action =>
                             methods.Contains(action.Method) && (result == null || action.Result.State == result.State))
                         .Where(action => action.Items.Any()))
            {
                return action.Items.First();
            }

            return default;
        }

        public List<T> Data(List<BatchMethod>? methods = null, BatchResult? result = null)
        {
            methods ??= BatchMethods.AllMethods;
            return IsEmpty
                ? new List<T>()
                : _actions
                    .Where(action =>
                        methods.Contains(action.Method) && (result == null || action.Result.State == result.State))
                    .SelectMany(action => action.Items)
                    .ToList();
        }
    }


    public abstract class BatchAware<T> where T : class, IIdentifiable
    {
        protected readonly BehaviorSubject<Dictionary<string, T>> Subject =
            new BehaviorSubject<Dictionary<string, T>>(new Dictionary<string, T>());

        public Observable<Dictionary<string, T>> ObservableData => Subject.AsObservable();

        public T? GetData(string key) =>
            Subject.Value.GetValueOrDefault(key);

        public bool Exists(string key) => Subject.Value.ContainsKey(key);

        public List<T> DataList => Subject.Value.Values.ToList();

        public bool IsDataEmpty => !Subject.Value.Any();

        public Dictionary<string, T> AllData => Subject.Value;

        public void Touch() => Subject.OnNext(Subject.Value);

        public void ProcessBatchData(BatchData<T> batchData)
        {
            foreach (var action in batchData.GetActions())
            {
                switch (action.Method)
                {
                    case BatchMethod.Insert:
                    case BatchMethod.Update:
                    case BatchMethod.Select:
                        var changedData = new Dictionary<string, T>(Subject.Value);
                        foreach (var entity in action.Items)
                        {
                            changedData[entity.Id] = entity;
                        }

                        Subject.OnNext(changedData);
                        break;

                    case BatchMethod.Delete:
                        var changedDict = new Dictionary<string, T>(Subject.Value);
                        var changed = false;
                        foreach (var entity in action.Items.Where(entity => changedDict.Remove(entity.Id)))
                        {
                            changed = true;
                        }

                        if (changed) Subject.OnNext(changedDict);
                        break;

                    case BatchMethod.Clear:
                        Subject.OnNext(new Dictionary<string, T>());
                        break;

                    case BatchMethod.Remove:
                        var copiedData = new Dictionary<string, T>(Subject.Value);
                        var reservedData = copiedData
                            .Where(pair => action.Where?.Invoke(pair.Value) != false)
                            .ToDictionary(pair => pair.Key, pair => pair.Value);
                        Subject.OnNext(reservedData);
                        break;
                    case BatchMethod.Replace:
                    case BatchMethod.Error:
                    default:
                        break;
                }
            }
        }

        public void DisposeBatch()
        {
            Subject.OnCompleted();
            Subject.Dispose();
        }
    }

    public interface IIdentifiable
    {
        string Id { get; }
    }

    public interface IGroupIdentifiable
    {
        string GroupId { get; } // 新增分组ID属性
        string ItemId { get; } // 原有条目ID属性
    }

    public abstract class GroupedBatchAware<T> where T : class, IGroupIdentifiable
    {
        private readonly BehaviorSubject<Dictionary<string, Dictionary<string, T>>> _groupedSubject =
            new(
                new Dictionary<string, Dictionary<string, T>>());

        // 获取指定分组的数据
        public Dictionary<string, T>? GetGroupData(string groupId) =>
            _groupedSubject.Value.GetValueOrDefault(groupId);

        // 获取指定分组的单个条目
        public T? GetGroupItem(string groupId, string itemId) =>
            GetGroupData(groupId)?.GetValueOrDefault(itemId);

        // 检查分组是否存在
        public bool ExistsGroup(string groupId) => _groupedSubject.Value.ContainsKey(groupId);

        public Observable<Dictionary<string, Dictionary<string, T>>> ObservableData => _groupedSubject.AsObservable();

        public Observable<Dictionary<string, T>> ObservableGroupData(string groupId) =>
            ObservableData.Select(ToGroupData(groupId));

        private static Func<Dictionary<string, Dictionary<string, T>>, Dictionary<string, T>> ToGroupData(
            string groupId)
        {
            return data => data.GetValueOrDefault(groupId) ?? new Dictionary<string, T>();
        }

        protected void ProcessBatchData(BatchData<T> batchData)
        {
            foreach (var action in batchData.GetActions())
            {
                var currentGroups = new Dictionary<string, Dictionary<string, T>>(_groupedSubject.Value);

                switch (action.Method)
                {
                    case BatchMethod.Insert:
                    case BatchMethod.Update:
                    case BatchMethod.Select:
                        foreach (var entity in action.Items)
                        {
                            if (!currentGroups.TryGetValue(entity.GroupId, out var groupDict))
                            {
                                groupDict = new Dictionary<string, T>();
                                currentGroups[entity.GroupId] = groupDict;
                            }

                            groupDict[entity.ItemId] = entity;
                        }

                        break;

                    case BatchMethod.Delete:
                        foreach (var entity in action.Items)
                        {
                            if (!currentGroups.TryGetValue(entity.GroupId, out var groupDict)) continue;
                            if (groupDict.Remove(entity.ItemId) && !groupDict.Any())
                            {
                                currentGroups.Remove(entity.GroupId);
                            }
                        }

                        break;

                    case BatchMethod.Clear:
                        currentGroups.Clear();
                        break;

                    case BatchMethod.Remove:
                        currentGroups = currentGroups
                            .ToDictionary(
                                g => g.Key,
                                g => g.Value.Where(pair => action.Where?.Invoke(pair.Value) != false)
                                    .ToDictionary(p => p.Key, p => p.Value)
                            )
                            .Where(g => g.Value.Any())
                            .ToDictionary(g => g.Key, g => g.Value);
                        break;
                    case BatchMethod.Replace:
                    case BatchMethod.Error:
                    default:
                        break;
                }

                _groupedSubject.AddEx(currentGroups);
            }
        }

        // ... existing DisposeBatch and other base methods ...
    }
}