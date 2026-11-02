using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using ZoomIN.App.Localization;
using ZoomIN.Core.Pointer;

namespace ZoomIN.App.Views.Controls;

/// <summary>
/// Colour choice for the pointer page: six preset swatches (pointer page decision) and a free
/// colour picker. <see cref="Color"/> is "#RRGGBB", as stored in the settings.
/// </summary>
public sealed partial class ColorChooser : UserControl
{
    public static readonly DependencyProperty ColorProperty = DependencyProperty.Register(
        nameof(Color), typeof(string), typeof(ColorChooser), new PropertyMetadata("#FFFFFF", OnColorChanged));

    private static readonly string[] s_presetNames = ["Yellow", "Green", "Cyan", "Magenta", "White", "Black"];

    private readonly List<(RgbColor Color, ToggleButton Button)> _swatches = [];
    private readonly ColorPicker _picker = new() { IsAlphaEnabled = false, IsMoreButtonVisible = true };
    private readonly Border _customSwatch = new() { Width = 16, Height = 16, CornerRadius = new CornerRadius(8) };
    private bool _updating;

    public ColorChooser()
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        for (var i = 0; i < RgbColor.Presets.Count; i++)
        {
            var color = RgbColor.Presets[i];
            var name = Text.Get("Color." + s_presetNames[i]);
            var button = new ToggleButton
            {
                Width = 32,
                Height = 32,
                Padding = new Thickness(0),
                Content = new Border
                {
                    Width = 20,
                    Height = 20,
                    CornerRadius = new CornerRadius(10),
                    Background = new SolidColorBrush(ToUi(color)),
                    BorderBrush = new SolidColorBrush(Colors.Gray),
                    BorderThickness = new Thickness(1),
                },
            };
            AutomationProperties.SetName(button, name);
            ToolTipService.SetToolTip(button, name);
            button.Click += (_, _) => Color = color.ToHex();
            _swatches.Add((color, button));
            panel.Children.Add(button);
        }

        var custom = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children = { _customSwatch, new TextBlock { Text = Text.Get("Color.Custom") } },
            },
            Flyout = new Flyout { Content = _picker },
        };
        AutomationProperties.SetName(custom, Text.Get("Color.Custom"));
        _picker.ColorChanged += (_, e) =>
        {
            if (!_updating)
            {
                Color = new RgbColor(e.NewColor.R, e.NewColor.G, e.NewColor.B).ToHex();
            }
        };
        panel.Children.Add(custom);
        Content = panel;
        Show(RgbColor.White);
    }

    public string Color
    {
        get => (string)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    private static Windows.UI.Color ToUi(RgbColor c) => Windows.UI.Color.FromArgb(255, c.R, c.G, c.B);

    private static void OnColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((ColorChooser)d).Show(RgbColor.ParseOrDefault(e.NewValue as string, RgbColor.White));

    private void Show(RgbColor color)
    {
        _updating = true;
        foreach (var (preset, button) in _swatches)
        {
            button.IsChecked = preset == color;
        }

        _customSwatch.Background = new SolidColorBrush(ToUi(color));
        _picker.Color = ToUi(color);
        _updating = false;
    }
}
