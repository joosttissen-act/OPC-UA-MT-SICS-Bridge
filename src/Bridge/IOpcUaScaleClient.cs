namespace Bridge;

/// <summary>
/// Abstraction over the OPC UA scale client, enabling unit testing without a
/// live OPC UA server.
/// </summary>
public interface IOpcUaScaleClient
{
    Task<double> ReadWeightAsync(CancellationToken ct = default);
    Task<bool> IsStableAsync(CancellationToken ct = default);
    Task TareAsync(CancellationToken ct = default);
}
