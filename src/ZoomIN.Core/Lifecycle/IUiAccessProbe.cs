namespace ZoomIN.Core.Lifecycle;

/// <summary>
/// Whether Windows granted this process UIAccess (ADR-0012). Without it the overlay lies below the
/// quick settings, Start and the notification center, which then stay visible unmagnified.
/// </summary>
public interface IUiAccessProbe
{
    bool IsActive { get; }
}
