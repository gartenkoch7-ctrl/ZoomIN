using ZoomIN.Core.Settings;
using ZoomIN.Core.Sharing;

namespace ZoomIN.App.Zoom;

/// <summary>Maps between persisted <see cref="AppSettings"/> and the runtime <see cref="ShareModel"/>.</summary>
internal static class ShareFactory
{
    public static ShareModel Create(AppSettings settings) => new(Options(settings));

    public static ShareOptions Options(AppSettings s) => new(s.ShareFilterEnabled, s.ShareAutoDetect, s.ShareColorFilter);

    public static AppSettings Apply(AppSettings settings, ShareModel model)
    {
        var o = model.Options;
        return settings with
        {
            ShareFilterEnabled = o.Enabled,
            ShareAutoDetect = o.AutoDetect,
            ShareColorFilter = o.Filter,
        };
    }
}
