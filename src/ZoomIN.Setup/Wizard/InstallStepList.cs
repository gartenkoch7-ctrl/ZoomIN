using System.Collections.ObjectModel;
using ZoomIN.Core.Updates;
using ZoomIN.Setup.Localization;

namespace ZoomIN.Setup.Wizard;

/// <summary>
/// Shows an <see cref="InstallChecklist"/> as the setup's step list with ticks. Every change also
/// yields a status line for the live region, so screen readers hear each step as it starts.
/// </summary>
public sealed class InstallStepList
{
    private const string PendingGlyph = "";
    private const string RunningGlyph = "";
    private const string DoneGlyph = "";
    private const string FailedGlyph = "";

    private string _version = string.Empty;

    public InstallStepList()
    {
        Checklist.Changed += Refresh;
        Refresh();
    }

    /// <summary>Raised with the text to announce after a step started or ended.</summary>
    public event Action<string>? StatusChanged;

    public InstallChecklist Checklist { get; } = new();

    public ObservableCollection<InstallStepItem> Items { get; } = [];

    /// <summary>Clears the ticks before (re)starting the installation of <paramref name="version"/>.</summary>
    public void Start(string version)
    {
        _version = version;
        Checklist.Reset();
    }

    /// <summary>Texts follow the language switch.</summary>
    public void Refresh()
    {
        Items.Clear();
        foreach (var step in InstallChecklist.Steps)
        {
            var state = Checklist.StateOf(step);
            Items.Add(new InstallStepItem(StepText(step), Glyph(state), SetupText.Get($"StepState.{state}")));
        }

        if (Checklist.Running is { } running)
        {
            StatusChanged?.Invoke(StepText(running) + " …");
        }
    }

    private string StepText(InstallStep step) => SetupText.Format($"InstallStep.{step}", _version);

    private static string Glyph(InstallStepState state) => state switch
    {
        InstallStepState.Running => RunningGlyph,
        InstallStepState.Done => DoneGlyph,
        InstallStepState.Failed => FailedGlyph,
        _ => PendingGlyph,
    };
}
