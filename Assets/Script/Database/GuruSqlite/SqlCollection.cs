#nullable enable
using Guru.SDK.Framework.Utils.Log;

namespace GuruSqlite
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    public abstract class QueryResult
    {
        public abstract int Count { get; }

        public abstract IEnumerator<QueryRow> GetEnumerator();
        
        public abstract QueryRow this[int index] { get; }

        public static QueryResult From(object queryResult)
        {
            return queryResult switch
            {
                Dictionary<string, object> dict => FromMap(dict),
                List<object> list => FromList(list),
                _ => throw new NotSupportedException($"Unsupported queryResult type {queryResult}")
            };
        }
        
        public static QueryResult FromMap(Dictionary<string, object> queryResultSetMap)
        {
            var columns = queryResultSetMap.TryGetValue("columns", out var value1)
                ? value1 as List<object>
                : null;
            var rows = queryResultSetMap.TryGetValue("rows", value: out var value)
                ? value as List<object>
                : null;
            return new StandardQueryResult(columns, rows);
        }
        
        public static QueryResult FromList(List<object> queryResultList)
        {
            List<EncapsulatedQueryRow> listRows = new();
            foreach (var obj in queryResultList)
            {
                if (obj is not Dictionary<object, object?> item) continue;
                var casted = new Dictionary<string, object>();
                foreach (var kv in item)
                {
                    if (kv.Value != null)
                    {
                        casted[Convert.ToString(kv.Key)!] = kv.Value;
                    }
                }
                listRows.Add(new EncapsulatedQueryRow(casted));
            }
            return new EncapsulatedQueryResult(listRows);

        }

        public static readonly QueryResult Empty = new EmptyQueryResult();

    }

    public abstract class QueryRow
    {
        public abstract object? this[string key] { get; }
    
        public abstract ICollection<string> Keys { get; }
    
        public abstract ICollection<object> Values { get; }


        public abstract bool ContainsKey(string key);

        public abstract bool TryGetValue(string key, out object? value);
    
        public abstract int Count { get; }
        
        public abstract IEnumerator<KeyValuePair<string, object?>> GetEnumerator();
        
        public static readonly QueryRow Empty = new EmptyQueryRow();
    }

    public class EmptyQueryRow : QueryRow
    {
        private readonly Dictionary<string, object> _emptyDictionary = new();
        public override object? this[string key] => _emptyDictionary[key];

        public override ICollection<string> Keys => _emptyDictionary.Keys;
        public override ICollection<object> Values => _emptyDictionary.Values;
        public override bool ContainsKey(string key)
        {
            return _emptyDictionary.ContainsKey(key);
        }

        public override bool TryGetValue(string key, out object? value)
        {
            return _emptyDictionary.TryGetValue(key, value: out value);
        }

        public override int Count => _emptyDictionary.Count;


        public override IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        {
            return _emptyDictionary.GetEnumerator();
        }
    }

    public class EmptyQueryResult : QueryResult
    {
        private readonly List<EmptyQueryRow> _emptyRows = new();
        public override int Count => 0;
        public override IEnumerator<QueryRow> GetEnumerator()
        {
            return _emptyRows.GetEnumerator();
        }

        public override QueryRow this[int index] => QueryRow.Empty;
    }
    
    /// <summary>
    /// Query native result set.
    /// </summary>
    public class StandardQueryResult: QueryResult
    {
        private readonly List<List<object>>? _rows;
        private readonly List<string>? _columns;
        private List<string>? _keys;
        private readonly Dictionary<string, int>? _columnIndexMap;

        internal StandardQueryResult(List<object>? rawColumns, List<object>? rawRows)
        {
            _columns = rawColumns?.Select(col => col.ToString()).ToList();
            if (rawRows != null)
            {
                _rows = new List<List<object>>();
                foreach (var rowList in rawRows.OfType<List<object>>())
                {
                    Log.D($"QueryResultSet: {rowList.Count}", "GuruSqlite");
                    _rows.Add(rowList);
                }
            }
    
            if (_columns == null) return;
            _columnIndexMap = new Dictionary<string, int>();
            for (var i = 0; i < _columns.Count; i++)
            {
                _columnIndexMap[_columns[i]] = i;
            }
            Log.D($"QueryResultSet columns: {_columns.Count}, rows: {_rows?.Count}");
        }
    
        public override int Count => _rows?.Count ?? 0;
    
        public override QueryRow this[int index]
        {
            get
            {
                if (index >= Count)
                {
                    return QueryRow.Empty;
                }

                var row = _rows?[index];
                return row == null ? QueryRow.Empty : new StandardQueryRow(this, row);
            }
        }

        public override IEnumerator<QueryRow> GetEnumerator()
        {
            for (var i = 0; i < Count; i++)
            {
                yield return this[i];
            }
        }
    
        /// <summary>
        /// Get the column index for a given column name.
        /// </summary>
        public int? ColumnIndex(string? name)
        {
            if (name == null || _columnIndexMap == null) return null;
            return _columnIndexMap.TryGetValue(name, out var value) ? value : (int?)null;
        }
    
        /// <summary>
        /// Remove duplicated.
        /// </summary>
        public List<string> Keys
        {
            get
            {
                if (_keys == null && _columns != null)
                {
                    _keys = _columns.Distinct().ToList();
                }
    
                return _keys ?? new List<string>();
            }
        }
    }
    
   
    /// <summary>
    /// Query row wrapper.
    /// </summary>
    public class StandardQueryRow : QueryRow
    {
        private readonly StandardQueryResult _queryResultSet;
        private readonly List<object> _row;
    
        public StandardQueryRow(StandardQueryResult queryResultSet, List<object> row)
        {
            _queryResultSet = queryResultSet;
            _row = row;
        }

        public override object? this[string key]
        {
            get
            {
                var columnIndex = _queryResultSet.ColumnIndex(key);
                Log.D($"QueryRow: key:{key} columnIndex:{columnIndex} _row.Count:{_row.Count}", "GuruSqlite");
                if (columnIndex == null) return null;
                return columnIndex < _row.Count ? _row[columnIndex.Value] : null;
            }
        }
    
        public override ICollection<string> Keys => _queryResultSet.Keys;

        public override ICollection<object> Values => _row;
    
        public override bool ContainsKey(string key) => _queryResultSet.ColumnIndex(key) != null;
    
        public override bool TryGetValue(string key, out object? value)
        {
            value = this[key];
            return value != null;
        }
    
        public override int Count => Keys.Count;
    
        public override IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        {
            return Keys.Select(k => new KeyValuePair<string, object?>(k, this[k])).GetEnumerator();
        }
    }

    public class EncapsulatedQueryRow : QueryRow
    {

        private readonly Dictionary<string, object> _rowData;

        internal EncapsulatedQueryRow(Dictionary<string, object> rowData)
        {
            _rowData = rowData;
        }

        public override object? this[string key] => TryGetValue(key, out var value) ? value : null;

        public override ICollection<string> Keys => _rowData.Keys;
        public override ICollection<object> Values => _rowData.Values;
        public override bool ContainsKey(string key)
        
        {
            return _rowData.ContainsKey(key);
        }

        public override bool TryGetValue(string key, out object? value)
        {
            return _rowData.TryGetValue(key, value: out value);
        }

        public override int Count => _rowData.Count;
        public override IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        {
            return _rowData.GetEnumerator();
        }
    }
    
    /// <summary>
    /// Native result wrapper.
    /// </summary>
    public class EncapsulatedQueryResult : QueryResult
    {
        private readonly List<EncapsulatedQueryRow> _rawList;
        public EncapsulatedQueryResult(List<EncapsulatedQueryRow> list)
        {
            _rawList = list;
        }

        public override int Count => _rawList.Count;

        public override IEnumerator<QueryRow> GetEnumerator()
        {
            for (var i = 0; i < Count; i++)
            {
                yield return this[i];
            }
        }

        public override QueryRow this[int index] => index >= Count ? QueryRow.Empty : _rawList[index];
    }
    
    

    
    /// <summary>
    /// Single batch operation results.
    /// </summary>
    public class BatchResult
    {
        public BatchResult(object? result)
        {
            Result = result;
        }
    
        public object? Result { get; }
    }
    
    /// <summary>
    /// Batch results.
    /// </summary>
    public class BatchResults 
    {
        
        private readonly List<Dictionary<object, object>> _list;
        public BatchResults(List<Dictionary<object, object>> list)
        {
            _list = list;
        }
    
        public static BatchResults From(List<object?> list)
        {
            // 将 List<object?> 转成 List<object> 以满足基类构造要求
            var dataList = new List<Dictionary<object, object>>();
            foreach (var o in list)
            {

                if (o is Dictionary<object, object> rawMap)
                {
                    dataList.Add(rawMap);
                }
            }

            return new BatchResults(dataList);
        }
    
        public  object? this[int index] => index >= _list.Count ? null : SqliteResultUtils.FromRawOperationResult(_list[index]);
    }

    /// <summary>
    /// Unpack the native results.
    /// </summary>
    public static class SqliteResultUtils
    {


        public static DatabaseException DatabaseExceptionFromOperationError(Dictionary<object, object> errorMap)
        {
            var message = errorMap.TryGetValue(Constants.ParamErrorMessage, out var msgObj)
                ? msgObj as string
                : null;
            var data = errorMap.GetValueOrDefault(Constants.ParamErrorData);
            var resultCode = errorMap.TryGetValue(Constants.ParamErrorResultCode, out var codeObj)
                ? codeObj as int?
                : null;
            return new SqliteDatabaseException(message, data, resultCode);
        }

        /// <summary>
        /// A batch operation result is either {'result':...} or {'error':...}.
        /// </summary>
        public static object? FromRawOperationResult(Dictionary<object, object> rawOperationResultMap)
        {
            if (rawOperationResultMap.TryGetValue(Constants.ParamError, out var errorObj))
            {
                if (errorObj is Dictionary<object, object> errorMap)
                {
                    return DatabaseExceptionFromOperationError(errorMap);
                }
            }

            if (!rawOperationResultMap.TryGetValue(Constants.ParamResult, out var successResult)) return null;
            return successResult switch
            {
                Dictionary<object, object> dict => QueryResult.From(dict),
                List<object> list => QueryResult.From(list),
                _ => successResult
            };
        }

        /// <summary>
        /// Native result to a map list as expected by the sqflite API.
        /// </summary>
        public static QueryResult QueryResultToList(object queryResult)
        {
            return queryResult switch
            {
                Dictionary<string, object> dict => QueryResult.FromMap(dict),
                List<object> list => QueryResult.FromList(list),
                _ => throw new NotSupportedException($"Unsupported queryResult type {queryResult}")
            };
        }

        /// <summary>
        /// Native result to a map list as expected by the sqflite API.
        /// </summary>
        public static int? QueryResultCursorId(object queryResult)
        {
            if (queryResult is Dictionary<object, object> dict &&
                dict.TryGetValue(Constants.ParamCursorId, out var cursorIdObj))
            {
                return cursorIdObj as int?;
            }

            throw new NotSupportedException($"Unsupported queryResult type {queryResult}");
        }
    }

    /// <summary>
    /// Placeholder for constants used in the code.
    /// </summary>
    public static class Constants
    {
        public const string ParamCursorId = "cursorId";
        public const string ParamErrorMessage = "errorMessage";
        public const string ParamErrorData = "errorData";
        public const string ParamErrorResultCode = "errorResultCode";
        public const string ParamError = "error";
        public const string ParamResult = "result";
    }
}