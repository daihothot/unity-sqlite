using Cysharp.Threading.Tasks;
using GuruSqlite;

namespace Guru.SDK.Framework.Utils.Database
{
    public static class MigrateResult
    {
        public const int Success = 0;
        public const int Failed = 1;
    }

    public interface IMigration
    {
        public UniTask<int> Migrate(SqliteTransaction transaction);
    }
}