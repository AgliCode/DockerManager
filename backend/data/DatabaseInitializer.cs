
using Dapper;
using Microsoft.Data.Sqlite;

namespace backend.Data;

public class DatabaseInitializer
{
    private readonly string _connectionString;

    public DatabaseInitializer(IConfiguration configuration)
    {
        _connectionString =
            configuration.GetConnectionString("HistoryDatabase")
            ?? "Data Source=history.db";
    }

    public async Task InitializeAsync()
    {
        await using var connection =
            new SqliteConnection(_connectionString);

        await connection.OpenAsync();

        const string sql = """
            CREATE TABLE IF NOT EXISTS LogCheckHistory
            (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CheckedAt TEXT NOT NULL,
                ContainersJson TEXT NOT NULL DEFAULT '[]',
                LogsText TEXT NOT NULL DEFAULT '',
                Filters TEXT NULL DEFAULT '[]'
            );
            """;

        await connection.ExecuteAsync(sql);
    }
}
