using Docker.DotNet;
using Docker.DotNet.Models;
namespace DockerManager.Services
{
    public sealed class ContainerMonitorService : BackgroundService
    {
        private readonly IDockerClient _dockerClient;
        private readonly ILogger<ContainerMonitorService> _logger;

        public event Action? ContainersChanged;

        public ContainerMonitorService(
            IDockerClient dockerClient,
            ILogger<ContainerMonitorService> logger)
        {
            _dockerClient = dockerClient;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var progress = new Progress<Message>(message =>
                    {
                        if (message.Type == "container")
                        {
                            _logger.LogInformation(
                                "Zdarzenie Docker: {Action}, kontener: {Id}",
                                message.Action,
                                message.ID);

                            ContainersChanged?.Invoke();
                        }
                    });

                    await _dockerClient.System.MonitorEventsAsync(
                        new ContainerEventsParameters(),
                        progress,
                        stoppingToken);
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
                        "Błąd nasłuchiwania zdarzeń Dockera.");

                    try
                    {
                        await Task.Delay(
                            TimeSpan.FromSeconds(3),
                            stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }
    }
}
