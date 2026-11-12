using ZoomIN.Core.Lifecycle;

namespace ZoomIN.Platform.Shell;

/// <summary>One Windows notification of the tray icon.</summary>
/// <param name="Clicked">Raised when the user clicks it; null = nothing.</param>
internal readonly record struct TrayNotice(string Title, string Message, TrayCommand? Clicked);
