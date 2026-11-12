using System.Globalization;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Islands;
using ZoomIN.Core.Localization;
using ZoomIN.Core.Shell;
using ZoomIN.Core.Viewport;

namespace ZoomIN.App.Islands;

/// <summary>
/// Tells the screen reader and the log what an edit step did (M19 B1), e.g. "Insel 2 von 4,
/// 3-fach". Only positions, sizes and levels, never content. Never throws: a failing announcement
/// must not break the edit mode.
/// </summary>
internal sealed class IslandReport(IAnnouncer announcer, ILocalizer texts, ILogger log)
{
    public void Outcome(IslandEditOutcome outcome, IslandEditor editor)
    {
        try
        {
            switch (outcome)
            {
                case IslandEditOutcome.Selected or IslandEditOutcome.Created or IslandEditOutcome.LevelChanged or IslandEditOutcome.Changed:
                    Island(outcome, editor.Set, editor.Selected);
                    break;
                case IslandEditOutcome.Full:
                    log.IslandRefused(IslandRules.MaxCount);
                    Say("Islands.Full");
                    break;
                case IslandEditOutcome.Deleted:
                    log.IslandRemoved(editor.Set.Count);
                    Say("Islands.Deleted");
                    break;
                case IslandEditOutcome.Cancelled:
                    Say("Islands.Cancelled");
                    break;
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            log.EditFailed(ex, "report");
        }
    }

    /// <summary>A new island turned hidden islands back on: logged and traced like Ctrl+Alt+I, spoken.</summary>
    public void ShownAgain(int count)
    {
        IslandOperation.Toggled(true, count);
        log.IslandsToggled(true, count);
        Say("Islands.On");
    }

    /// <summary>Speaks a text without arguments; never throws.</summary>
    public void Say(string key)
    {
        try
        {
            announcer.Announce(texts.Get(key));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            log.EditFailed(ex, "announce");
        }
    }

    private void Island(IslandEditOutcome outcome, IslandSet set, int index)
    {
        if (index < 0 || index >= set.Count)
        {
            return;
        }

        var island = set[index];
        if (outcome != IslandEditOutcome.Selected)
        {
            var name = outcome.ToString();
            var source = Describe(island.Source);
            var picture = Describe(island.Target);
            log.IslandChanged(index + 1, name, island.MonitorId, source, picture, island.Level, island.Still);
        }

        // Spoken when picked and after every change: which one, of how many, how far magnified.
        var level = island.Level.ToString("0.#", CultureInfo.CurrentCulture);
        announcer.Announce(texts.Format("Islands.Island", index + 1, set.Count, level));
    }

    private static string Describe(PhysicalRect rect) =>
        string.Create(CultureInfo.InvariantCulture, $"{rect.X:0},{rect.Y:0} {rect.Width:0}x{rect.Height:0}");
}
