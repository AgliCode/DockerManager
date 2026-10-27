using Docker.DotNet;
using Dapper;
using Docker.DotNet.Models;
using Microsoft.Data.Sqlite;
using backend.Models;
using System.IO;
using System.Text;
namespace backend.Services;

public sealed class LogMonitoringService : BackgroundService
{
    private readonly IDockerClient _dockerClient;
    private readonly ILogger<LogMonitoringService> _logger;
    private readonly string _connectionString;

    public LogMonitoringService(
        IDockerClient dockerClient,
        IConfiguration configuration,
        ILogger<LogMonitoringService> logger)
    {
        _dockerClient = dockerClient;
        _logger = logger;

        _connectionString =
            configuration.GetConnectionString("DockerManagerDatabase")
            ?? "Data Source=dockermanager.db";
    }

    private readonly Dictionary<string, StreamWorker> _streams =
    new(StringComparer.Ordinal);

    private sealed class StreamWorker
    {
        public required CancellationTokenSource Cancellation { get; init; }
        public required Task Task { get; init; }
    }

    //Odczytywanie strumienia linia po linii
    private async Task ReadLogStreamAsync(string containerId, CancellationToken cancellationToken)
    {
        var lineCount = 0;
        var pending = new StringBuilder();
        var decoder = Encoding.UTF8.GetDecoder();
        var charBuffer = new char[8192];
        var byteBuffer = new byte[4096];

        try
        {
            using var stream =
                await OpenLogStreamAsync(containerId, cancellationToken);

            _logger.LogInformation(
                "Nasłuchiwanie logów kontenera {ContainerId} rozpoczęte.",
                containerId);

            while (!cancellationToken.IsCancellationRequested)
            {
                var result = await stream.ReadOutputAsync(
                    byteBuffer,
                    0,
                    byteBuffer.Length,
                    cancellationToken);

                if (result.EOF)
                    break;

                if (result.Count == 0)
                    continue;

                var charCount = decoder.GetChars(
                    byteBuffer,
                    0,
                    result.Count,
                    charBuffer,
                    0,
                    flush: false);

                pending.Append(charBuffer, 0, charCount);

                while (true)
                {
                    var text = pending.ToString();
                    var newlineIndex = text.IndexOf('\n');

                    if (newlineIndex < 0)
                        break;

                    var line = text[..newlineIndex].TrimEnd('\r');
                    pending.Remove(0, newlineIndex + 1);

                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    lineCount++;

                    _logger.LogInformation(
                        "[TEST LICZNIKA] Kontener: {ContainerId} | Liczba nowych linii: {Count} | Log: {LogLine}",
                        containerId,
                        lineCount,
                        line);
                }
            }

            // Obsłuż ewentualną ostatnią linię bez znaku nowej linii.
            if (pending.Length > 0)
            {
                var line = pending.ToString().TrimEnd('\r');

                if (!string.IsNullOrWhiteSpace(line))
                {
                    lineCount++;

                    _logger.LogInformation(
                        "[TEST LICZNIKA] Kontener: {ContainerId} | Liczba nowych linii: {Count} | Log: {LogLine}",
                        containerId,
                        lineCount,
                        line);
                }
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // Strumień został celowo zatrzymany.
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Nie udało się odczytywać logów kontenera {ContainerId}.",
                containerId);
        }
    }

    //Funkcja która otwiera strumień dla konkretnego kontenera
    private async Task<MultiplexedStream> OpenLogStreamAsync(string containerId, CancellationToken cancellationToken)
    {
        var parameters = new ContainerLogsParameters
        {
            ShowStdout = true,
            ShowStderr = true,
            Follow = true,
            Timestamps = true,
            Tail = "0"
        };

        return await _dockerClient.Containers.GetContainerLogsAsync(
            containerId,
            false,
            parameters,
            cancellationToken);
    }

    //Analizujemy tylko monitoringi, które są włączone
    private async Task<List<Monitoring>> GetEnabledMonitoringsAsync()
    {
        await using var connection =
            new SqliteConnection(_connectionString);

        const string sql = """
        SELECT
            Id,
            Name,
            ContainerId,
            ContainerName,
            KeywordsJson,
            NotificationsJson,
            AlertInterval,
            IsEnabled,
            CreatedAt
        FROM Monitorings
        WHERE IsEnabled = 1;
        """;

        var monitorings =
            await connection.QueryAsync<Monitoring>(sql);

        return monitorings.ToList();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Uruchomiono usługę monitorowania logów.");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var monitorings = await GetEnabledMonitoringsAsync();

                    var activeContainerIds = monitorings
                        .Select(m => m.ContainerId)
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .Distinct(StringComparer.Ordinal)
                        .ToHashSet(StringComparer.Ordinal);

                    // Zatrzymaj strumienie, które nie są już potrzebne.
                    foreach (var entry in _streams.ToList())
                    {
                        if (!activeContainerIds.Contains(entry.Key)
                            || entry.Value.Task.IsCompleted)
                        {
                            entry.Value.Cancellation.Cancel();

                            try
                            {
                                await entry.Value.Task;
                            }
                            catch (OperationCanceledException)
                            {
                                // Oczekiwane podczas zatrzymywania strumienia.
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(
                                    ex,
                                    "Strumień kontenera {ContainerId} zakończył się błędem.",
                                    entry.Key);
                            }

                            entry.Value.Cancellation.Dispose();
                            _streams.Remove(entry.Key);
                        }
                    }

                    // Uruchom strumienie tylko dla nowych kontenerów.
                    foreach (var containerId in activeContainerIds)
                    {
                        if (_streams.ContainsKey(containerId))
                            continue;

                        var cts =
                            CancellationTokenSource.CreateLinkedTokenSource(
                                stoppingToken);

                        var task = ReadLogStreamAsync(
                            containerId,
                            cts.Token);

                        _streams[containerId] = new StreamWorker
                        {
                            Cancellation = cts,
                            Task = task
                        };

                        _logger.LogInformation(
                            "Uruchomiono strumień dla kontenera {ContainerId}.",
                            containerId);
                    }

                    _logger.LogInformation(
                        "Aktywne konfiguracje: {MonitoringCount}; aktywne strumienie: {StreamCount}.",
                        monitorings.Count,
                        _streams.Count);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Błąd podczas aktualizacji aktywnych monitoringów.");
                }

                await Task.Delay(
                    TimeSpan.FromSeconds(5),
                    stoppingToken);
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Normalne zakończenie usługi.
        }
        finally
        {
            foreach (var worker in _streams.Values)
                worker.Cancellation.Cancel();

            foreach (var worker in _streams.Values)
            {
                try
                {
                    await worker.Task;
                }
                catch (OperationCanceledException)
                {
                    // Normalne zakończenie strumienia.
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Błąd podczas zamykania strumienia.");
                }
                finally
                {
                    worker.Cancellation.Dispose();
                }
            }

            _streams.Clear();
        }
    }


}
