using CommunityToolkit.Mvvm.ComponentModel;
using ZoomIN.App.Localization;
using ZoomIN.App.Updates;
using ZoomIN.App.Views;

namespace ZoomIN.App.ViewModels;

/// <summary>
/// Banner on the overview while ZoomIN runs without UIAccess (ADR-0017): explains why quick
/// settings and Start stay visible unmagnified and offers to install the signed build.
/// </summary>
public sealed partial class UiAccessBannerViewModel : ObservableObject
{
    private readonly PageContext _context;

    public UiAccessBannerViewModel(PageContext context)
    {
        _context = context;
        IsOpen = !context.UiAccess.IsActive;
    }

    public bool IsOpen { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInstall))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string Status { get; set; } = string.Empty;

    public bool CanInstall => !IsBusy;

    public async Task InstallAsync()
    {
        IsBusy = true;
        Status = Text.Get("UiAccess.Downloading");
        try
        {
            var result = await _context.Setup.LaunchAsync(null, CancellationToken.None);
            Status = Text.Get($"UiAccess.Result.{result}");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
