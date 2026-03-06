namespace Bridge;

/// <summary>
/// Parses raw MT-SICS command strings received from the MES TCP client.
/// </summary>
public static class MtSicsCommandParser
{
    /// <summary>
    /// Normalises a raw line received from the TCP stream into an upper-case
    /// command token (e.g. "SI", "S", "T").  CR/LF characters and surrounding
    /// whitespace are stripped before comparison.
    /// </summary>
    public static string Parse(string rawLine)
    {
        if (rawLine is null)
            return string.Empty;

        return rawLine.Trim('\r', '\n', ' ').ToUpperInvariant();
    }
}
