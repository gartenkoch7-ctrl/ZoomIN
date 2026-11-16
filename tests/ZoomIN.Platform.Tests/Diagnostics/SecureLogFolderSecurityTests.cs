using System.Security.AccessControl;
using System.Security.Principal;
using ZoomIN.Platform.Diagnostics;

namespace ZoomIN.Platform.Tests.Diagnostics;

/// <summary>
/// The access list of the folder the elevated copy writes into (M10, Q7): the user who asked may
/// read and delete it, but never add an entry (a link would redirect the administrator's writes)
/// or change the list.
/// </summary>
public sealed class SecureLogFolderSecurityTests
{
    private const FileSystemRights Forbidden =
        FileSystemRights.WriteData | FileSystemRights.AppendData | FileSystemRights.WriteAttributes
        | FileSystemRights.WriteExtendedAttributes | FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership;

    [Fact]
    public void TheUser_MayReadAndDelete_ButNotWrite()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var rules = SecureLogCopy.TargetSecurity().GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().ToList();

        var user = Assert.Single(rules, r => r.IdentityReference.Equals(identity.User));
        Assert.Equal(AccessControlType.Allow, user.AccessControlType);
        Assert.Equal(FileSystemRights.ReadAndExecute, user.FileSystemRights & FileSystemRights.ReadAndExecute);
        Assert.Equal(FileSystemRights.Delete, user.FileSystemRights & FileSystemRights.Delete);
        Assert.Equal((FileSystemRights)0, user.FileSystemRights & Forbidden);
    }

    [Fact]
    public void OnlySystemAndAdministrators_HaveFullControl_AndOwnIt()
    {
        var security = SecureLogCopy.TargetSecurity();
        var full = security.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>()
            .Where(r => r.FileSystemRights.HasFlag(FileSystemRights.FullControl))
            .Select(r => (SecurityIdentifier)r.IdentityReference)
            .ToList();

        Assert.Equal(2, full.Count);
        Assert.Contains(full, sid => sid.IsWellKnown(WellKnownSidType.LocalSystemSid));
        Assert.Contains(full, sid => sid.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid));
        Assert.True(((SecurityIdentifier)security.GetOwner(typeof(SecurityIdentifier))!).IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid));
        Assert.True(security.AreAccessRulesProtected);
    }
}
