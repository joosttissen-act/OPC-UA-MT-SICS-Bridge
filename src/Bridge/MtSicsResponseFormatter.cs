namespace Bridge;

/// <summary>
/// Formats MT-SICS protocol responses.
/// All responses are ASCII-encoded and terminated with CR+LF as required by
/// the MT-SICS specification.
/// </summary>
public static class MtSicsResponseFormatter
{
    /// <summary>
    /// Formats a weight response.
    /// </summary>
    /// <param name="weight">Weight value in kilograms.</param>
    /// <param name="stable">
    /// <c>true</c> if the reading is stable (→ status "S");
    /// <c>false</c> for a dynamic reading (→ status "D").
    /// </param>
    /// <returns>MT-SICS formatted weight line including CR+LF.</returns>
    public static string FormatWeight(double weight, bool stable)
    {
        var status = stable ? "S" : "D";
        return $"S {status} {weight,10:0.000} kg\r\n";
    }

    /// <summary>
    /// Formats a tare-success response.
    /// </summary>
    public static string FormatTareSuccess() => "T S\r\n";

    /// <summary>
    /// Formats an error response meaning "command understood but execution
    /// failed" (e.g. OPC UA read/write fault).
    /// </summary>
    /// <param name="commandLetter">The single command letter ("S" or "T").</param>
    public static string FormatExecutionError(string commandLetter) =>
        $"{commandLetter} I\r\n";

    /// <summary>
    /// Formats the response for an unrecognised command.
    /// </summary>
    public static string FormatUnknownCommand() => "I\r\n";
}
