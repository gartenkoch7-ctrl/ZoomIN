using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace ZoomIN.App.Views.Controls;

/// <summary>
/// Slider with its value and unit shown next to it (e.g. "2,5 ×"). The same text is the
/// slider's spoken value, so screen readers hear the unit too.
/// </summary>
public sealed partial class ValueSlider : UserControl
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(ValueSlider), new PropertyMetadata(0.0, OnValueChanged));

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum), typeof(double), typeof(ValueSlider), new PropertyMetadata(0.0, OnRangeChanged));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(ValueSlider), new PropertyMetadata(100.0, OnRangeChanged));

    public static readonly DependencyProperty StepProperty = DependencyProperty.Register(
        nameof(Step), typeof(double), typeof(ValueSlider), new PropertyMetadata(1.0, OnRangeChanged));

    /// <summary>Composite format for the value, e.g. "{0:0} %" or "{0:0.0} ×".</summary>
    public static readonly DependencyProperty FormatProperty = DependencyProperty.Register(
        nameof(Format), typeof(string), typeof(ValueSlider), new PropertyMetadata("{0:0}", OnValueChanged));

    private readonly Slider _slider = new() { Width = 200, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _text = new() { MinWidth = 64, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Right };

    public ValueSlider()
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        panel.Children.Add(_text);
        panel.Children.Add(_slider);
        Content = panel;
        _slider.ValueChanged += (_, e) => Value = e.NewValue;
        _slider.ThumbToolTipValueConverter = null;
        _slider.IsThumbToolTipEnabled = false;
        AutomationProperties.SetAccessibilityView(_text, Microsoft.UI.Xaml.Automation.Peers.AccessibilityView.Raw);
    }

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double Step
    {
        get => (double)GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    public string Format
    {
        get => (string)GetValue(FormatProperty);
        set => SetValue(FormatProperty, value);
    }

    /// <summary>The inner slider, so the card can give it the accessible name.</summary>
    internal Slider Slider => _slider;

    private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var self = (ValueSlider)d;
        self._slider.Minimum = self.Minimum;
        self._slider.Maximum = self.Maximum;
        self._slider.StepFrequency = self.Step;
        self._slider.SmallChange = self.Step;
        self._slider.Value = self.Value;
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var self = (ValueSlider)d;
        if (self._slider.Value != self.Value)
        {
            self._slider.Value = self.Value;
        }

        var text = string.Format(CultureInfo.CurrentCulture, self.Format, self.Value);
        self._text.Text = text;
        AutomationProperties.SetItemStatus(self._slider, text);
    }
}
