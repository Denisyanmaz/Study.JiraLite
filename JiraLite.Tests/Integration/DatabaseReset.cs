using Npgsql;
using Respawn;
using Respawn.Graph;

namespace JiraLite.Tests.Integration
{
    public class DatabaseReset
    {
        private readonly string _connectionString;
        private Respawner? _respawner;

        public DatabaseReset(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task InitializeAsync()
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            _respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
            {
                DbAdapter = DbAdapter.Postgres,
                SchemasToInclude = new[] { "public" },
                WithReseed = true
            });
        }

        public async Task ResetAsync()
        {
            if (_respawner == null)
                await InitializeAsync();

            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            await _respawner!.ResetAsync(conn);
        }
    }
}
