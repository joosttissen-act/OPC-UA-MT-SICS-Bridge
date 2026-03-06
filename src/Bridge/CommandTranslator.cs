using Microsoft.Extensions.Logging;

namespace Bridge;

/// <summary>
/// Translates MT-SICS commands into OPC UA operations and returns formatted
/// MT-SICS response strings.
/// </summary>
public sealed class CommandTranslator
{
    private readonly IOpcUaScaleClient _scaleClient;
    private readonly ILogger<CommandTranslator> _logger;

    public CommandTranslator(IOpcUaScaleClient scaleClient, ILogger<CommandTranslator> logger)
    {
        _scaleClient = scaleClient;
        _logger = logger;
    }

    /// <summary>
    /// Handles a single MT-SICS command and returns the formatted response.
    /// </summary>
    public async Task<string> HandleCommandAsync(string command, CancellationToken ct = default)
    {
        _logger.LogInformation("Received command: {Command}", command);

        return command switch
        {
            "SI" => await HandleImmediateWeightAsync(ct),
            "S"  => await HandleWeightAsync(ct),
            "T"  => await HandleTareAsync(ct),
            _    => HandleUnknown(command)
        };
    }

    // -------------------------------------------------------------------------

    private async Task<string> HandleImmediateWeightAsync(CancellationToken ct)
    {
        try
        {
            var weight = await _scaleClient.ReadWeightAsync(ct);
            var stable = await _scaleClient.IsStableAsync(ct);
            _logger.LogInformation("Weight read: {Weight} kg (stable={Stable})", weight, stable);
            return MtSicsResponseFormatter.FormatWeight(weight, stable);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read weight/stability for SI command");
            return MtSicsResponseFormatter.FormatExecutionError("S");
        }
    }

    private async Task<string> HandleWeightAsync(CancellationToken ct)
    {
        try
        {
            var weight = await _scaleClient.ReadWeightAsync(ct);
            _logger.LogInformation("Weight read: {Weight} kg", weight);
            // S command returns the weight without waiting for stability –
            // use stable=false (status "D") to indicate a dynamic reading.
            return MtSicsResponseFormatter.FormatWeight(weight, stable: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read weight for S command");
            return MtSicsResponseFormatter.FormatExecutionError("S");
        }
    }

    private async Task<string> HandleTareAsync(CancellationToken ct)
    {
        try
        {
            await _scaleClient.TareAsync(ct);
            return MtSicsResponseFormatter.FormatTareSuccess();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute tare");
            return MtSicsResponseFormatter.FormatExecutionError("T");
        }
    }

    private string HandleUnknown(string command)
    {
        _logger.LogWarning("Unknown command received: {Command}", command);
        return MtSicsResponseFormatter.FormatUnknownCommand();
    }
}
