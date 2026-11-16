namespace ZoomIN.Core.Monitors;

/// <summary>What a frozen monitor shows (G2), the setting "Inhalt eingefrorener Monitore".</summary>
public enum FrozenContent
{
    /// <summary>The frozen view keeps showing the live desktop (a video in it keeps playing). Factory setting.</summary>
    Live,

    /// <summary>The image at the moment of freezing stays (building block "Standbild").</summary>
    Still,
}
