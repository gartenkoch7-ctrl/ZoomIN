using ZoomIN.Core.Updates;

namespace ZoomIN.Platform.Tests.Updates;

/// <summary>Records what the install step would sign; never touches the machine's certificate stores.</summary>
internal sealed class FakeLocalSigner : ILocalSigner
{
    public bool Succeeds { get; set; } = true;

    public List<string> Signed { get; } = [];

    public List<string> Kept { get; } = [];

    public bool Sign(string executablePath)
    {
        Signed.Add(executablePath);
        return Succeeds;
    }

    public int Prunes { get; private set; }

    public void Prune(IEnumerable<string> keptExecutables)
    {
        Prunes++;
        Kept.AddRange(keptExecutables);
    }

    public void RemoveAll()
    {
    }
}
