namespace ZoomIN.Core.Tracking;

/// <summary>Receives the hold key on the hook thread (E1); every method must return quickly and not allocate.</summary>
public interface IHoldKeyHandler
{
    /// <summary>The key to watch; read for every key event.</summary>
    MarkerHoldKey Key { get; }

    /// <summary>The hold key went down (also sent again by key repeat).</summary>
    void HoldKeyDown();

    /// <summary>The hold key was released.</summary>
    void HoldKeyUp();

    /// <summary>Any other key went down; while the hold key is held, this is typing, not holding.</summary>
    void OtherKeyDown();
}
