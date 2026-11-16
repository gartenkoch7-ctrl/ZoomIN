namespace ZoomIN.Core.Updates;

/// <summary>Checks the Authenticode signature of an executable before it is installed.</summary>
public interface ISignatureVerifier
{
    /// <returns><see cref="PackageCheck.Ok"/> only if Windows trusts the signature and the signer is ZoomIN.</returns>
    PackageCheck Verify(string executablePath);
}
