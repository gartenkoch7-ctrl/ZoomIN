namespace ZoomIN.Platform.Updates;

/// <summary>Signs one file with a certificate from the machine store (ADR-0031); behind an interface per ADR-0025.</summary>
internal interface ISigningProcess
{
    /// <returns>0 if the signature is valid, otherwise an exit code.</returns>
    int Run(string executablePath, string thumbprint);
}
