using System.Net;

namespace ZoomIN.Core.Diagnostics;

/// <summary>
/// Judges an OTLP export address for the diagnostics page (ADR-0019): empty means nothing leaves
/// the PC; an address on this PC is quiet; anything else gets a warning, because logs and
/// operations then travel over the network.
/// </summary>
public static class TelemetryEndpoint
{
    /// <summary>Empty, or an absolute http/https address.</summary>
    public static bool IsValid(string? address) =>
        string.IsNullOrWhiteSpace(address) || TryParse(address, out _);

    /// <summary>True for localhost, 127.0.0.0/8 and ::1.</summary>
    public static bool IsLocal(string? address)
    {
        if (!TryParse(address, out var uri))
        {
            return false;
        }

        if (uri.IsLoopback || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(uri.Host.Trim('[', ']'), out var ip) && IPAddress.IsLoopback(ip);
    }

    private static bool TryParse(string? address, out Uri uri)
    {
        uri = null!;
        return !string.IsNullOrWhiteSpace(address)
            && Uri.TryCreate(address.Trim(), UriKind.Absolute, out uri!)
            && uri.Scheme is "http" or "https";
    }
}
