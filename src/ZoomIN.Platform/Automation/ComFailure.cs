using System.Runtime.InteropServices;

namespace ZoomIN.Platform.Automation;

/// <summary>
/// Accessibility calls go into other applications, which may answer anything. COM interop turns a
/// failed HRESULT into a matching .NET exception, not always a <see cref="COMException"/>:
/// E_INVALIDARG becomes <see cref="ArgumentException"/>, E_NOINTERFACE
/// <see cref="InvalidCastException"/>, E_NOTIMPL <see cref="NotImplementedException"/> and
/// E_ACCESSDENIED <see cref="UnauthorizedAccessException"/>. Catching only COMException let a
/// Chromium window answering E_INVALIDARG end ZoomIN (2026-11-09).
/// </summary>
internal static class ComFailure
{
    /// <summary>True when <paramref name="exception"/> is a failed answer of another application.</summary>
    public static bool IsAnswer(Exception exception) =>
        exception is COMException
            or InvalidCastException
            or ArgumentException
            or NotImplementedException
            or UnauthorizedAccessException;
}
