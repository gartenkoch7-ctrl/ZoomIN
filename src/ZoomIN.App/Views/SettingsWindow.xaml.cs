using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;
using ZoomIN.App.Localization;
using ZoomIN.App.Views.Pages;

namespace ZoomIN.App.Views;

/// <summary>
/// Settings window (M7). Closing hides it to the tray; only "Exit" in the tray closes it for real.
/// Pages get the <see cref="PageContext"/> as navigation parameter.
/// </summary>
public sealed partial class SettingsWindow : Window
{
    private readonly PageContext _context;
    private bool _exiting;
    private Type _currentPage;

    public SettingsWindow(PageContext context)
    {
        _context = context;
        InitializeComponent();
        AppWindow.Resize(new SizeInt32(980, 720));
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "ZoomIN.ico"));
        AppWindow.Closing += OnClosing;
        Text.LanguageChanged += OnLanguageChanged;

        _currentPage = context.Settings.Current.FirstRunCompleted ? typeof(OverviewPage) : typeof(WelcomePage);
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
        Add(Text.NavInfo, "", typeof(InfoPage));

        var current = Navigation.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(i => (Type)i.Tag == _currentPage);
        if (current is not null)
        {
            Navigation.SelectedItem = current;
        }

        // Always navigate again so the page texts follow a language change.
        ContentFrame.Navigate(_currentPage, _context);
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
            ContentFrame.Navigate(page, _context);
        }
    }

    /// <summary>Used by the first-run assistant when it is done.</summary>
    internal void ShowOverview()
    {
        _currentPage = typeof(OverviewPage);
        BuildNavigation();
    }
}
