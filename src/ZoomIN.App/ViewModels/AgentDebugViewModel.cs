using CommunityToolkit.Mvvm.ComponentModel;
using ZoomIN.App.Settings;

namespace ZoomIN.App.ViewModels;

/// <summary>"Allow KI-Debug" on the diagnostics section (ADR-0027): the pipe exists only while it is on.</summary>
public sealed partial class AgentDebugViewModel(SettingsService settings) : ObservableObject
{
    public bool Allowed
    {
        get => settings.Current.AgentDebugEnabled;
        set
        {
            if (value == Allowed)
            {
                return;
            }

            settings.Update(s => s with { AgentDebugEnabled = value });
            OnPropertyChanged();
        }
    }
}
