using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace ZoomIN.App.Views.Controls;

/// <summary>
/// One settings row in the style of the Windows 11 Settings app: icon, title and description on
/// the left, the control (the content) on the right. Template in App.xaml.
/// </summary>
public sealed partial class SettingCard : ContentControl
{
    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header), typeof(string), typeof(SettingCard), new PropertyMetadata(string.Empty, OnTextChanged));

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description), typeof(string), typeof(SettingCard), new PropertyMetadata(string.Empty, OnTextChanged));

    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph), typeof(string), typeof(SettingCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DescriptionVisibilityProperty = DependencyProperty.Register(
        nameof(DescriptionVisibility), typeof(Visibility), typeof(SettingCard), new PropertyMetadata(Visibility.Collapsed));

    private string _namedAs = string.Empty;

    public SettingCard()
    {
        DefaultStyleKey = typeof(SettingCard);
    }

    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>Segoe Fluent Icons glyph.</summary>
    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public Visibility DescriptionVisibility
    {
        get => (Visibility)GetValue(DescriptionVisibilityProperty);
        private set => SetValue(DescriptionVisibilityProperty, value);
    }

    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);
        NameContent();
    }

    // Screen readers announce the control with the card title (and the description as help).
    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var card = (SettingCard)d;
        card.DescriptionVisibility = string.IsNullOrEmpty(card.Description) ? Visibility.Collapsed : Visibility.Visible;
        card.NameContent();
    }

    private void NameContent()
    {
        var control = Content switch
        {
            ValueSlider slider => slider.Slider,
            TextBlock => null,
            DependencyObject other => other,
            _ => null,
        };
        // A button says what it does ("Reset now"): it keeps that name, and the card title moves
        // into the help text – named after the card, NVDA read only the title (found 2026-11-05).
        if (control is ButtonBase button)
        {
            AutomationProperties.SetHelpText(button, string.IsNullOrEmpty(Description) ? Header : $"{Header}. {Description}");
            return;
        }

        if (control is not null)
        {
            if (string.IsNullOrEmpty(AutomationProperties.GetName(control)) || AutomationProperties.GetName(control) == _namedAs)
            {
                AutomationProperties.SetName(control, Header);
                _namedAs = Header;
            }

            AutomationProperties.SetHelpText(control, Description);
        }
    }
}
