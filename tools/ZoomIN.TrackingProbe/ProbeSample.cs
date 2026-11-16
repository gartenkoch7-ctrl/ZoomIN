using System.Globalization;
using ZoomIN.Core.Viewport;

namespace ZoomIN.TrackingProbe;

/// <summary>What every focus/caret source reported for the foreground application at one moment.</summary>
internal sealed record ProbeSample(
    string Process,
    bool IsChromium,
    PhysicalRect? Focus,
    PhysicalRect? SystemCaret,
    PhysicalRect? UiaCaret,
    PhysicalRect? MsaaCaret)
{
    public string Describe() => string.Create(
        CultureInfo.InvariantCulture,
        $"{Process,-18}{(IsChromium ? " [Chromium]" : string.Empty)}  Fokus {Format(Focus)}  System {Format(SystemCaret)}  UIA {Format(UiaCaret)}  MSAA {Format(MsaaCaret)}");

    private static string Format(PhysicalRect? r) => r is { } v
        ? string.Create(CultureInfo.InvariantCulture, $"{v.X:0},{v.Y:0} {v.Width:0}x{v.Height:0}")
        : "–";
}
