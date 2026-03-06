namespace Bridge;

/// <summary>
/// Configuration options for the OPC UA connection and node IDs.
/// </summary>
public sealed class OpcUaOptions
{
    public const string SectionName = "OpcUa";

    /// <summary>OPC UA server endpoint URL, e.g. "opc.tcp://localhost:4840".</summary>
    public string EndpointUrl { get; set; } = "opc.tcp://localhost:4840";

    /// <summary>Node ID for the weight value (e.g. "ns=2;s=Scale.Weight").</summary>
    public string WeightNode { get; set; } = "ns=2;s=Scale.Weight";

    /// <summary>Node ID for the stability flag (e.g. "ns=2;s=Scale.Stable").</summary>
    public string StabilityNode { get; set; } = "ns=2;s=Scale.Stable";

    /// <summary>Node ID for the tare command (e.g. "ns=2;s=Scale.Tare").</summary>
    public string TareNode { get; set; } = "ns=2;s=Scale.Tare";

    /// <summary>Seconds to wait before attempting to reconnect after a session drop.</summary>
    public int ReconnectDelaySeconds { get; set; } = 5;

    /// <summary>Number of times to retry a read operation before returning an error.</summary>
    public int ReadRetryCount { get; set; } = 3;
}

/// <summary>
/// Configuration options for the MT-SICS TCP server.
/// </summary>
public sealed class MtSicsServerOptions
{
    public const string SectionName = "MtSicsServer";

    /// <summary>TCP port on which the bridge listens for MES connections.</summary>
    public int Port { get; set; } = 8001;
}
