using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bridge;

/// <summary>
/// Background hosted service that owns the lifecycle of the
/// <see cref="MtSicsTcpServer"/> and performs OPC UA reconnection retries.
/// </summary>
public sealed class ScaleService : BackgroundService
{
    private readonly MtSicsTcpServer _tcpServer;
    private readonly OpcUaOptions _opcUaOptions;
    private readonly ILogger<ScaleService> _logger;

    public ScaleService(
        MtSicsTcpServer tcpServer,
        IOptions<OpcUaOptions> opcUaOptions,
        ILogger<ScaleService> logger)
    {
        _tcpServer = tcpServer;
        _opcUaOptions = opcUaOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ScaleService starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _tcpServer.RunAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "MT-SICS server encountered an error. Restarting in {Delay}s",
                    _opcUaOptions.ReconnectDelaySeconds);

                await Task.Delay(
                    TimeSpan.FromSeconds(_opcUaOptions.ReconnectDelaySeconds),
                    stoppingToken);
            }
        }

        _logger.LogInformation("ScaleService stopped");
    }
}
