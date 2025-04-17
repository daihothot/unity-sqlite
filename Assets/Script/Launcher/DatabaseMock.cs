
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Guru.SDK.Framework.Utils.Database;
using GuruSqlite;

public class DataBaseMock : AppDatabase
{
    protected override List<Func<SqliteTransaction, UniTask<bool>>> TableCreators => new List<Func<SqliteTransaction, UniTask<bool>>>();

    protected override string DbName => "DatabaseMock";

    protected override int Version => 1;

    protected override List<IMigration> Migrations => new List<IMigration>();
} 