namespace ZoomIN.Core.Islands;

/// <summary>
/// The input of the island edit mode (M19 B1): while it runs, mouse buttons, the wheel, Esc and Del
/// go to ZoomIN and not to applications; pointer moves go on to Windows as always. Outside the edit
/// mode nothing is taken (plan decision: clicks on islands fall through).
/// </summary>
public interface IIslandEditInput
{
    /// <summary>
    /// The input can be taken (the low-level hooks are installed). Without it the edit mode must not
    /// start: the user's clicks would reach the applications under it.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>Starts taking the input. Handler calls come one at a time on a background thread, never on the hook thread.</summary>
    void Start(IIslandEditInputHandler handler);

    /// <summary>Stops at once; a button or key held now still has its release kept from applications.</summary>
    void Stop();
}
