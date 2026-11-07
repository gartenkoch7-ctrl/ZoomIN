using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;
using ZoomIN.App.Localization;
using ZoomIN.App.Views.Pages;

namespace ZoomIN.App.Views;

/// <summary>
/// Settings window (M7). Closing hides it to the tray; only "Exit" in the tray closes it for real.
/// Pages get <see cref="PageServices"/> as navigation parameter.
/// </summary>
public sealed partial class SettingsWindow : Window
{
    private readonly PageServices _pages;
    private bool _exiting;
    private Type _currentPage;

    public SettingsWindow(PageServices pages, bool firstRunCompleted)
    {
        _pages = pages;
        InitializeComponent();
        AppWindow.Resize(new SizeInt32(980, 720));
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "ZoomIN.ico"));

        // The system title bar of a WinUI 3 window stays light even in dark mode unless told to
        // follow the Windows app mode; the content already does.
        AppWindow.TitleBar.PreferredTheme = TitleBarTheme.UseDefaultAppMode;
        AppWindow.Closing += OnClosing;
        Text.LanguageChanged += OnLanguageChanged;

        _currentPage = firstRunCompleted ? typeof(OverviewPage) : typeof(WelcomePage);
        BuildNavigation();
    }

    /// <summary>Raised when the user closed the window and ZoomIN keeps running in the tray.</summary>
    public event Action? HiddenToTray;

    public nint Handle => WinRT.Interop.WindowNative.GetWindowHandle(this);

    public void ShowAndActivate()
    {
        AppWindow.Show();
        Activate();
    }

    public void CloseForExit()
    {
        _exiting = true;
        Text.LanguageChanged -= OnLanguageChanged;
        Close();
    }

    private void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_exiting)
        {
            return;
        }

        args.Cancel = true;
        sender.Hide();
        HiddenToTray?.Invoke();
    }

    private void OnLanguageChanged() => DispatcherQueue.TryEnqueue(BuildNavigation);

    private void BuildNavigation()
    {
        Title = "ZoomIN";
        Navigation.MenuItems.Clear();
        Add(Text.Get("NavOverview"), "", typeof(OverviewPage));
        Add(Text.NavZoom, "", typeof(ZoomPage));
        Add(Text.Get("NavFilter"), "", typeof(FilterPage));
        Add(Text.Get("NavPointer"), "", typeof(PointerPage));
        Add(Text.NavTracking, "", typeof(TrackingPage));
        Add(Text.NavHotkeys, "", typeof(HotkeysPage));
        Add(Text.NavProfiles, "", typeof(ProfilesPage));
        Add(Text.NavGeneral, "", typeof(GeneralPage));
        Add(Text.Get("NavUpdates"), "", typeof(UpdatesPage));
        Add(Text.NavInfo, "", typeof(InfoPage));

        var current = Navigation.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(i => (Type)i.Tag == _currentPage);
        if (current is not null)
        {
            Navigation.SelectedItem = current;
        }

        // Always navigate again so the page texts follow a language change.
        ContentFrame.Navigate(_currentPage, _pages);
    }

    private void Add(string text, string glyph, Type page) => Navigation.MenuItems.Add(new NavigationViewItem
    {
        Content = text,
        Icon = new FontIcon { Glyph = glyph },
        Tag = page,
    });

    private void OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem { Tag: Type page } && page != ContentFrame.CurrentSourcePageType)
        {
            _currentPage = page;
            ContentFrame.Navigate(page, _pages);
        }
    }

    /// <summary>Used by the first-run assistant when it is done.</summary>
    internal void ShowOverview()
    {
        _currentPage = typeof(OverviewPage);
        BuildNavigation();
    }
}
