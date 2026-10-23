using Docker.DotNet;
using Docker.DotNet.Models;
using backend.Data;
using backend.Models;
using Dapper;
using Microsoft.Data.Sqlite;
using System.Text.Json;
using backend.Services;
using backend.Hubs;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<DatabaseInitializer>();

// Połączenie react - backend
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Połączenie z docker
builder.Services.AddSingleton<IDockerClient>(_ =>
{
    var dockerUri = OperatingSystem.IsWindows()
        ? new Uri("npipe://./pipe/docker_engine")
        : new Uri("unix:///var/run/docker.sock");

    return new DockerClientConfiguration(dockerUri).CreateClient();
});

//Rejestracja serwisu do nasłuchiwania zmian na żywo
builder.Services.AddSingleton<ContainerMonitorService>();

builder.Services.AddHostedService(sp =>
    sp.GetRequiredService<ContainerMonitorService>());

builder.Services.AddSignalR();

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var databaseInitializer =
        scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();

    await databaseInitializer.InitializeAsync();
}
app.UseCors();
app.MapHub<ContainerHub>("/containerHub");
app.MapGet("/api/containers", async (IDockerClient dockerClient) =>
{
    try
    {
        var containers = await dockerClient.Containers.ListContainersAsync(
            new ContainersListParameters
            {
                All = true
            });

        return Results.Ok(containers);
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.Message);

        return Results.Problem(
            title: "Nie udało się pobrać kontenerów z Dockera.",
            statusCode: 500);
    }
});

app.MapPost("/api/containers/logs", async (
    GetLogsRequest request,
    IDockerClient dockerClient) =>
{
    if (request.ContainerIds == null ||
        request.ContainerIds.Count == 0)
    {
        return Results.BadRequest(
            new { error = "Nie wybrano żadnego kontenera." });
    }
    var containers = await dockerClient.Containers.ListContainersAsync(
       new ContainersListParameters
       {
           All = true
       });

    var result = new System.Text.StringBuilder();

    foreach (var id in request.ContainerIds)
    {
        var container = containers.FirstOrDefault(c => c.ID == id);

        string name = container != null
            ? string.Join(", ", container.Names).Replace("/", "")
            : id[..Math.Min(id.Length, 12)];

        result.AppendLine($"========== {name} ==========");
        result.AppendLine();

        try
        {
            var parameters = new ContainerLogsParameters
            {
                ShowStdout = true,
                ShowStderr = true,
                Timestamps = true,
                Follow = false
            };

            using var stream =
                await dockerClient.Containers.GetContainerLogsAsync(
                    id,
                    false,
                    parameters
                );

            var (stdout, stderr) =
                await stream.ReadOutputToEndAsync(
                    CancellationToken.None
                );

            if (!string.IsNullOrWhiteSpace(stdout))
                result.AppendLine(stdout);

            if (!string.IsNullOrWhiteSpace(stderr))
                result.AppendLine(stderr);

            if (string.IsNullOrWhiteSpace(stdout) &&
                string.IsNullOrWhiteSpace(stderr))
            {
                result.AppendLine("Brak logów do wyświetlenia.");
            }
        }
        catch (Exception ex)
        {
            result.AppendLine($"Błąd pobierania logów: {ex.Message}");
        }

        result.AppendLine();
    }
    try
    {
        var connectionString =
            app.Configuration.GetConnectionString("HistoryDatabase")
            ?? "Data Source=history.db";

        var selectedContainers = request.ContainerIds
            .Select(id =>
            {
                var container = containers.FirstOrDefault(c => c.ID == id);

                return new
                {
                    Id = id,
                    Name = container != null
                        ? string.Join(", ", container.Names).Replace("/", "")
                        : id[..Math.Min(id.Length, 12)]
                };
            })
            .ToList();

        await using var connection =
            new SqliteConnection(connectionString);

        const string sql = """
            INSERT INTO LogCheckHistory
                (CheckedAt, ContainersJson, LogsText, Filters)
            VALUES
                (@CheckedAt, @ContainersJson, @LogsText, @Filters);
            """;

        await connection.ExecuteAsync(sql, new
        {
            CheckedAt = DateTime.Now,
            ContainersJson = JsonSerializer.Serialize(selectedContainers),
            LogsText = result.ToString(),
            Filters = "[]"
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Błąd zapisu historii: {ex.Message}");
    }
    return Results.Ok(new { logs = result.ToString() });
});

app.MapGet("/api/history", async (IConfiguration configuration) =>
{
    try
    {
        var connectionString =
            configuration.GetConnectionString("HistoryDatabase")
            ?? "Data Source=history.db";

        await using var connection =
            new SqliteConnection(connectionString);

        const string sql = """
            SELECT Id, CheckedAt, ContainersJson, LogsText, Filters
            FROM LogCheckHistory
            ORDER BY CheckedAt DESC, Id DESC;
            """;

        var history = await connection.QueryAsync<LogCheckHistory>(sql);

        return Results.Ok(history);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Błąd pobierania historii: {ex.Message}");

        return Results.Problem(
            title: "Nie udało się pobrać historii logów.",
            statusCode: 500);
    }
});


app.Run();

record GetLogsRequest(List<string> ContainerIds);

