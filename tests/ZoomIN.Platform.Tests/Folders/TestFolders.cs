using ZoomIN.Core.Folders;

namespace ZoomIN.Platform.Tests.Folders;

/// <summary>ZoomIN folders in a test directory: Local is the directory itself, Roaming a subfolder.</summary>
internal sealed record TestFolders(string Root) : IAppFolders
{
    public string Local => Root;

    public string Roaming => Path.Combine(Root, "roaming");
}
