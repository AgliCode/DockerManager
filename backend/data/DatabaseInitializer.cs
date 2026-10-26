
using Dapper;
using Microsoft.Data.Sqlite;

namespace backend.Data;

public class DatabaseInitializer
{
    private readonly string _connectionString;

    public DatabaseInitializer(IConfiguration configuration)
    {
        _connectionString =
            configuration.GetConnectionString("DockerManagerDatabase")
            ?? "Data Source=dockermanager.db";
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

            CREATE TABLE IF NOT EXISTS Monitorings
            (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                ContainerId TEXT NOT NULL,
                ContainerName TEXT NOT NULL,
                KeywordsJson TEXT NOT NULL DEFAULT '[]',
                NotificationsJson TEXT NOT NULL DEFAULT '[]',
                AlertInterval INTEGER NOT NULL DEFAULT 12,
                IsEnabled INTEGER NOT NULL DEFAULT 1,
                CreatedAt TEXT NOT NULL
            );
            """;

        await connection.ExecuteAsync(sql);
    }
}
