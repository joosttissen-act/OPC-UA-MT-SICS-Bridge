using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bridge;

/// <summary>
/// Listens for MT-SICS TCP connections from the MES system, reads line-based
/// commands and writes back MT-SICS formatted responses.
/// </summary>
public sealed class MtSicsTcpServer : IAsyncDisposable
{
    private readonly MtSicsServerOptions _options;
    private readonly CommandTranslator _translator;
    private readonly ILogger<MtSicsTcpServer> _logger;

    private TcpListener? _listener;

    public MtSicsTcpServer(
        IOptions<MtSicsServerOptions> options,
        CommandTranslator translator,
        ILogger<MtSicsTcpServer> logger)
    {
        _options = options.Value;
        _translator = translator;
        _logger = logger;
    }

    /// <summary>
    /// Starts accepting TCP connections until <paramref name="ct"/> is cancelled.
    /// </summary>
    public async Task RunAsync(CancellationToken ct)
    {
        _listener = new TcpListener(IPAddress.Any, _options.Port);
        _listener.Start();
        _logger.LogInformation("MT-SICS TCP server listening on port {Port}", _options.Port);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(ct);
                // Handle each client on its own task so the server stays responsive.
                _ = HandleClientAsync(client, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        finally
        {
            _listener.Stop();
            _logger.LogInformation("MT-SICS TCP server stopped");
        }
    }

    // -------------------------------------------------------------------------

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        var remoteEndPoint = client.Client.RemoteEndPoint;
        _logger.LogInformation("MES connected from {Endpoint}", remoteEndPoint);

        using (client)
        {
            await using var stream = client.GetStream();

            // MT-SICS is ASCII / line-based.  We read a character at a time so we
            // can detect the CR LF terminator without buffering whole chunks.
            var lineBuffer = new StringBuilder();
            var byteBuffer = new byte[1];

            try
            {
                while (!ct.IsCancellationRequested && client.Connected)
                {
                    int bytesRead = await stream.ReadAsync(byteBuffer, ct);
                    if (bytesRead == 0)
                        break; // Client disconnected.

                    char ch = (char)byteBuffer[0];

                    if (ch == '\n')
                    {
                        // End of line – process the buffered command.
                        var raw = lineBuffer.ToString();
                        lineBuffer.Clear();

                        var command = MtSicsCommandParser.Parse(raw);
                        if (string.IsNullOrEmpty(command))
                            continue;

                        var response = await _translator.HandleCommandAsync(command, ct);
                        var bytes = Encoding.ASCII.GetBytes(response);
                        await stream.WriteAsync(bytes, ct);
                    }
                    else if (ch != '\r')
                    {
                        lineBuffer.Append(ch);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Shutdown requested.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling client {Endpoint}", remoteEndPoint);
            }
            finally
            {
                _logger.LogInformation("MES disconnected from {Endpoint}", remoteEndPoint);
            }
        }
    }

    // -------------------------------------------------------------------------

    public ValueTask DisposeAsync()
    {
        _listener?.Stop();
        return ValueTask.CompletedTask;
    }
}
