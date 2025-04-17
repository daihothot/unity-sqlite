using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace GuruSqlite
{
    #nullable enable

    /// <summary>
    /// SQLite查询游标实现
    /// </summary>
    internal class SqliteQueryCursor : IQueryCursor
    {
        private readonly SqliteDatabase _db;
        private readonly int _cursorId;
        private bool _closed = false;
        private Dictionary<string, object?>? _current;

        /// <summary>
        /// 构造函数
        /// </summary>
        public SqliteQueryCursor(SqliteDatabase db, int cursorId)
        {
            _db = db;
            _cursorId = cursorId;
        }

        /// <summary>
        /// 获取当前行数据
        /// </summary>
        public Dictionary<string, object?> Current
        {
            get
            {
                if (_closed)
                {
                    throw new InvalidOperationException("Cursor is closed");
                }
                
                if (_current == null)
                {
                    throw new InvalidOperationException("No current row. Call MoveNext() first.");
                }
                
                return _current;
            }
        }

        /// <summary>
        /// 移动到下一行
        /// </summary>
        public async UniTask<bool> MoveNext()
        {
            if (_closed)
            {
                throw new InvalidOperationException("Cursor is closed");
            }
            
            var args = new object[] { _cursorId };
            var hasNext = await _db.InvokeMethod<bool>("queryCursorMoveNext", args);
            
            if (hasNext)
            {
                _current = await _db.InvokeMethod<Dictionary<string, object?>>("queryCursorGetCurrent", args);
            }
            else
            {
                _current = null;
            }
            
            return hasNext;
        }

        /// <summary>
        /// 关闭游标
        /// </summary>
        public async UniTask Close()
        {
            if (!_closed)
            {
                try
                {
                    await _db.InvokeMethod<object>("queryCursorClose", new object[] { _cursorId });
                }
                finally
                {
                    _closed = true;
                    _current = null;
                }
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Close().Forget();
        }
    }
} 