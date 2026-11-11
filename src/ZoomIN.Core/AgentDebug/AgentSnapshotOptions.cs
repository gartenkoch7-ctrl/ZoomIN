using System.Globalization;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.AgentDebug;

/// <summary>Arguments of <see cref="AgentTools.TakeSnapshot"/>, checked.</summary>
/// <param name="Kinds">Image kinds to return: "result", "original", "weights".</param>
/// <param name="Monitor">Only this monitor ("DISPLAY1"), or null for all.</param>
/// <param name="Crop">Part of each image in monitor pixels, or null for all of it.</param>
/// <param name="MaxWidth">Scale down to this width, or 0 for no scaling.</param>
public sealed record AgentSnapshotOptions(IReadOnlyList<string> Kinds, string? Monitor, PhysicalRect? Crop, int MaxWidth)
{
    public static bool TryParse(AgentRequest request, out AgentSnapshotOptions options, out string error)
    {
        ArgumentNullException.ThrowIfNull(request);
        options = new AgentSnapshotOptions(["result"], null, null, 0);
        error = string.Empty;

        var images = request.Argument(AgentTools.ImagesParameter)?.ToLowerInvariant() ?? "result";
        if (!AgentTools.ImageChoices.Contains(images))
        {
            error = "images must be one of: " + string.Join(", ", AgentTools.ImageChoices);
            return false;
        }

        IReadOnlyList<string> kinds = images == "all" ? ["result", "original", "weights"] : [images];

        PhysicalRect? crop = null;
        if (request.Argument(AgentTools.CropParameter) is { } cropText)
        {
            if (!TryParseRect(cropText, out var rect))
            {
                error = "crop must be 'x,y,width,height' with width and height > 0";
                return false;
            }

            crop = rect;
        }

        var maxWidth = 0;
        if (request.Argument(AgentTools.MaxWidthParameter) is { } widthText
            && (!int.TryParse(widthText, NumberStyles.None, CultureInfo.InvariantCulture, out maxWidth) || maxWidth < 16))
        {
            error = "max_width must be a whole number of at least 16";
            return false;
        }

        // "\\.\DISPLAY1" and "DISPLAY1" mean the same monitor.
        var monitor = request.Argument(AgentTools.MonitorParameter)?.TrimStart('\\', '.');
        options = new AgentSnapshotOptions(kinds, monitor, crop, maxWidth);
        return true;
    }

    private static bool TryParseRect(string text, out PhysicalRect rect)
    {
        rect = default;
        var parts = text.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 4)
        {
            return false;
        }

        var values = new int[4];
        for (var i = 0; i < 4; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out values[i]))
            {
                return false;
            }
        }

        if (values[0] < 0 || values[1] < 0 || values[2] <= 0 || values[3] <= 0)
        {
            return false;
        }

        rect = new PhysicalRect(values[0], values[1], values[2], values[3]);
        return true;
    }
}
