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
app.MapGet("/api/containers", async (
    IDockerClient dockerClient,
    ILogger < Program > logger) =>
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
        logger.LogError(ex, "Nie udało się pobrać kontenerów z Dockera.");

        return Results.Problem(
            title: "Nie udało się pobrać kontenerów z Dockera.",
            statusCode: 500);
    }
});

app.MapPost("/api/containers/logs", async (
    GetLogsRequest request,
    IDockerClient dockerClient,
    ILogger<Program> logger) =>
{
    if (request.ContainerIds == null ||
        request.ContainerIds.Count == 0)
    {
        return Results.BadRequest(
            new { error = "Nie wybrano żadnego kontenera." });
    }
    IList<ContainerListResponse> containers;
    try
    {
        containers = await dockerClient.Containers.ListContainersAsync(
        new ContainersListParameters
        {
          All = true
        });

    }
    catch (Exception ex)
    {
        logger.LogError(
           ex,
           "Nie udało się pobrać listy kontenerów z Dockera podczas pobierania logów.");

        return Results.Problem(
            title: "Nie udało się pobrać listy kontenerów z Dockera.",
            statusCode: 500);
    }

    var result = new System.Text.StringBuilder();

    var containerLogs = new List<object>();

    foreach (var id in request.ContainerIds)
    {
        var container = containers.FirstOrDefault(c => c.ID == id);

        string name = container != null
            ? string.Join(", ", container.Names).Replace("/", "")
            : id[..Math.Min(id.Length, 12)];

        var singleContainerLogs = new System.Text.StringBuilder();

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
                singleContainerLogs.AppendLine(stdout);

            if (!string.IsNullOrWhiteSpace(stderr))
                singleContainerLogs.AppendLine(stderr);

            if (string.IsNullOrWhiteSpace(stdout) &&
                string.IsNullOrWhiteSpace(stderr))
            {
                singleContainerLogs.AppendLine("Brak logów do wyświetlenia.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Nie udało się pobrać logów kontenera {ContainerId} ({ContainerName})",
                id,
                name
            );
            singleContainerLogs.AppendLine(
               $"Błąd pobierania logów: {ex.Message}"
            );
        }
        string logs = singleContainerLogs.ToString();

        result.Append(logs);
        result.AppendLine();

        containerLogs.Add(new
        {
            id,
            name,
            logs
        });
    }
    try
    {
        var connectionString =
            app.Configuration.GetConnectionString("DockerManagerDatabase")
            ?? "Data Source=dockermanager.db";

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
        logger.LogError(ex, "Nie udało się zapisać historii logów.");
    }
    return Results.Ok(new { logs = result.ToString(), containerLogs = containerLogs });
});

app.MapGet("/api/history", async (
    IConfiguration configuration,
    ILogger<Program> logger) =>
{
    try
    {
        var connectionString =
            configuration.GetConnectionString("DockerManagerDatabase")
            ?? "Data Source=dockermanager.db";

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
        logger.LogError(ex, "Nie udało się pobrać historii logów.");

        return Results.Problem(
            title: "Nie udało się pobrać historii logów.",
            statusCode: 500);
    }
});

app.MapGet("/api/monitoring", async (
    IConfiguration configuration,
    ILogger<Program> logger) =>
{
    try
    {
        var connectionString =
            configuration.GetConnectionString("DockerManagerDatabase")
            ?? "Data Source=dockermanager.db";

        await using var connection =
            new SqliteConnection(connectionString);

        const string sql = """
            SELECT Id, Name, ContainerId, ContainerName, KeywordsJson,
            NotificationsJson, AlertInterval, IsEnabled, CreatedAt
            FROM Monitorings
            ORDER BY CreatedAt DESC, Id DESC;
            """;

        var monitoring = await connection.QueryAsync<Monitoring>(sql);
        return Results.Ok(monitoring);

    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Nie udało się pobrać konfiguracji monitoringów.");

        return Results.Problem(
            title: "Nie udało się pobrać konfiguracji monitoringów.",
            statusCode: 500);
    }
});

app.MapPost("/api/monitoring", async (
    CreateMonitoringRequest request,
    IConfiguration configuration,
    ILogger<Program> logger) =>
{
    try
    {
        var connectionString =
            configuration.GetConnectionString("DockerManagerDatabase")
            ?? "Data Source=dockermanager.db";

        await using var connection =
            new SqliteConnection(connectionString);

        const string sql = """
            INSERT INTO Monitorings
                (Name, ContainerId, ContainerName, KeywordsJson, NotificationsJson, AlertInterval, IsEnabled, CreatedAt)
            VALUES
                (@Name, @ContainerId, @ContainerName, @KeywordsJson, @NotificationsJson, @AlertInterval, @IsEnabled, @CreatedAt);
            """;

        await connection.ExecuteAsync(sql, new
        {
            Name = request.Name,
            ContainerId = request.ContainerId,
            ContainerName = request.ContainerName,
            KeywordsJson = request.KeywordsJson,
            NotificationsJson = request.NotificationsJson,
            AlertInterval = request.AlertInterval,
            IsEnabled = request.IsEnabled,
            CreatedAt = DateTime.Now,
        });

        return Results.Created("/api/monitoring", null);

    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Nie udało się zapisać konfiguracji monitoringu.");

        return Results.Problem(
            title: "Nie udało się zapisać konfiguracji monitoringu.",
            statusCode: 500);
    }
});

Console.WriteLine("DockerManager API uruchamia się...");
app.Run();

record GetLogsRequest(List<string> ContainerIds);
record CreateMonitoringRequest(
    string Name,
    string ContainerId,
    string ContainerName,
    string KeywordsJson,
    string NotificationsJson,
    int AlertInterval,
    bool IsEnabled
);

