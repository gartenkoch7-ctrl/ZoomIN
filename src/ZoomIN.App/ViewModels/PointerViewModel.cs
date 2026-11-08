using CommunityToolkit.Mvvm.ComponentModel;
using ZoomIN.App.Zoom;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Pointer;

namespace ZoomIN.App.ViewModels;

/// <summary>
/// Pointer page: size, colour, contrast rim, locator, crosshair and hiding while typing.
/// Every change applies live and updates the preview.
/// </summary>
public sealed partial class PointerViewModel : ObservableObject
{
    private readonly EffectsModel _effects;
    private readonly HotkeyBindings _hotkeys;
    private readonly bool _ready;

    public PointerViewModel(EffectsModel effects, HotkeyBindings hotkeys)
    {
        _effects = effects;
        _hotkeys = hotkeys;
        var p = effects.Current.Cursor;
        MaxScale = p.MaxScale;
        UnzoomedScale = p.UnzoomedScale;
        OverrideWindows = p.OverrideWindows;
        GrowWithZoom = p.GrowWithZoom;
        ColorModeIndex = (int)p.ColorMode;
        Color = p.Color.ToHex();
        AdaptiveOnDark = p.AdaptiveOnDark.ToHex();
        AdaptiveOnLight = p.AdaptiveOnLight.ToHex();
        AdaptiveThreshold = Math.Round(p.AdaptiveThreshold * 100);
        Rim = p.Rim;
        RimColor = p.RimColor.ToHex();
        RimWidth = p.RimWidth;
        LocatorColor = p.LocatorColor.ToHex();
        LocatorSize = p.LocatorSize;
        LocatorSeconds = p.LocatorSeconds;
        TriggerHotkey = p.LocatorTriggers.HasFlag(LocatorTriggers.Hotkey);
        TriggerShake = p.LocatorTriggers.HasFlag(LocatorTriggers.Shake);
        TriggerIdle = p.LocatorTriggers.HasFlag(LocatorTriggers.AfterIdle);
        TriggerCtrl = p.LocatorTriggers.HasFlag(LocatorTriggers.CtrlKey);
        CrosshairIndex = !p.CrosshairEnabled ? 0 : p.CrosshairAlways ? 2 : 1;
        CrosshairColor = p.CrosshairColor.ToHex();
        CrosshairWidth = p.CrosshairWidth;
        CrosshairOpacity = Math.Round(p.CrosshairOpacity * 100);
        HideWhileTyping = p.HideWhileTyping;
        _ready = true;
    }

    /// <summary>Raised after every change; the page redraws the preview.</summary>
    public event Action? StyleChanged;

    public PointerStyle Style => _effects.Current.Cursor;

    public bool UsesColor => ColorModeIndex == (int)PointerColorMode.Tint;

    /// <summary>The pointer follows the background (M15): colour pair and threshold apply.</summary>
    public bool IsAdaptive => ColorModeIndex == (int)PointerColorMode.Adaptive;

    public bool CrosshairOn => CrosshairIndex > 0;

    [ObservableProperty]
    public partial double MaxScale { get; set; }

    [ObservableProperty]
    public partial double UnzoomedScale { get; set; }

    [ObservableProperty]
    public partial bool OverrideWindows { get; set; }

    [ObservableProperty]
    public partial bool GrowWithZoom { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UsesColor))]
    [NotifyPropertyChangedFor(nameof(IsAdaptive))]
    public partial int ColorModeIndex { get; set; }

    [ObservableProperty]
    public partial string Color { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AdaptiveOnDark { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AdaptiveOnLight { get; set; } = string.Empty;

    /// <summary>Threshold in percent (10–90).</summary>
    [ObservableProperty]
    public partial double AdaptiveThreshold { get; set; }

    [ObservableProperty]
    public partial bool Rim { get; set; }

    [ObservableProperty]
    public partial string RimColor { get; set; } = string.Empty;

    [ObservableProperty]
    public partial double RimWidth { get; set; }

    [ObservableProperty]
    public partial string LocatorColor { get; set; } = string.Empty;

    [ObservableProperty]
    public partial double LocatorSize { get; set; }

    [ObservableProperty]
    public partial double LocatorSeconds { get; set; }

    [ObservableProperty]
    public partial bool TriggerHotkey { get; set; }

    [ObservableProperty]
    public partial bool TriggerShake { get; set; }

    [ObservableProperty]
    public partial bool TriggerIdle { get; set; }

    [ObservableProperty]
    public partial bool TriggerCtrl { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CrosshairOn))]
    public partial int CrosshairIndex { get; set; }

    [ObservableProperty]
    public partial string CrosshairColor { get; set; } = string.Empty;

    [ObservableProperty]
    public partial double CrosshairWidth { get; set; }

    [ObservableProperty]
    public partial double CrosshairOpacity { get; set; }

    [ObservableProperty]
    public partial bool HideWhileTyping { get; set; }

    /// <summary>"Suchhilfe testen": shows the locator ring at the pointer.</summary>
    public void TestLocator() => _effects.Locate();

    /// <summary>"Farben tauschen": the colour for dark ground becomes the one for light ground and back.</summary>
    public void SwapAdaptiveColors() => (AdaptiveOnDark, AdaptiveOnLight) = (AdaptiveOnLight, AdaptiveOnDark);

    public void Reset()
    {
        _effects.SetCursor(PointerStyle.Default);
        _hotkeys.Register();
    }

    // CommunityToolkit calls OnPropertyChanged for every generated property: one place to apply.
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (!_ready || e.PropertyName is nameof(UsesColor) or nameof(IsAdaptive) or nameof(CrosshairOn))
        {
            return;
        }

        var hotkeyBefore = Style.LocatorTriggers.HasFlag(LocatorTriggers.Hotkey);
        _effects.SetCursor(Build());
        if (hotkeyBefore != TriggerHotkey)
        {
            _hotkeys.Register();
        }

        StyleChanged?.Invoke();
    }

    private PointerStyle Build()
    {
        var d = PointerStyle.Default;
        var triggers = (TriggerHotkey ? LocatorTriggers.Hotkey : 0)
            | (TriggerShake ? LocatorTriggers.Shake : 0)
            | (TriggerIdle ? LocatorTriggers.AfterIdle : 0)
            | (TriggerCtrl ? LocatorTriggers.CtrlKey : 0);
        return new PointerStyle
        {
            MaxScale = MaxScale,
            UnzoomedScale = UnzoomedScale,
            OverrideWindows = OverrideWindows,
            GrowWithZoom = GrowWithZoom,
            ColorMode = ColorModeIndex >= 0 ? (PointerColorMode)ColorModeIndex : d.ColorMode,
            Color = RgbColor.ParseOrDefault(Color, d.Color),
            AdaptiveOnDark = RgbColor.ParseOrDefault(AdaptiveOnDark, d.AdaptiveOnDark),
            AdaptiveOnLight = RgbColor.ParseOrDefault(AdaptiveOnLight, d.AdaptiveOnLight),
            AdaptiveThreshold = AdaptiveThreshold / 100,
            Rim = Rim,
            RimColor = RgbColor.ParseOrDefault(RimColor, d.RimColor),
            RimWidth = RimWidth,
            LocatorColor = RgbColor.ParseOrDefault(LocatorColor, d.LocatorColor),
            LocatorSize = LocatorSize,
            LocatorSeconds = LocatorSeconds,
            LocatorTriggers = triggers,
            CrosshairEnabled = CrosshairIndex > 0,
            CrosshairAlways = CrosshairIndex == 2,
            CrosshairColor = RgbColor.ParseOrDefault(CrosshairColor, d.CrosshairColor),
            CrosshairWidth = CrosshairWidth,
            CrosshairOpacity = CrosshairOpacity / 100,
            HideWhileTyping = HideWhileTyping,
        };
    }
}
