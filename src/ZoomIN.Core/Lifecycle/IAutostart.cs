namespace ZoomIN.Core.Lifecycle;

/// <summary>Start ZoomIN when the user signs in to Windows (asked in the first-run assistant, M7).</summary>
public interface IAutostart
{
    bool IsEnabled { get; }

    void SetEnabled(bool enabled);
}
