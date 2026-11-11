using System.Text.RegularExpressions;
using ZoomIN.Rendering.Gpu;

namespace ZoomIN.Rendering.Tests.Gpu;

/// <summary>
/// Shaders are compiled during the build (M14 step 4). Every ShaderSource.Load in the code must have
/// its bytecode embedded, or that pipeline would fail to start (log 3003) on the user's PC.
/// </summary>
public sealed partial class ShaderBytecodeTests
{
    [Fact]
    public void EveryLoadedShader_IsEmbeddedAsBytecode()
    {
        var loads = RenderingSources()
            .SelectMany(text => LoadCall().Matches(text))
            .Select(m => (File: m.Groups["file"].Value, Entry: m.Groups["entry"].Value, Profile: m.Groups["profile"].Value))
            .Distinct()
            .ToList();

        Assert.Equal(10, loads.Count);
        foreach (var (file, entry, profile) in loads)
        {
            var bytecode = ShaderSource.Load(file, entry, profile);

            // Compiled D3D shaders start with the container tag "DXBC".
            Assert.True(bytecode.Length > 4 && bytecode[0] == 'D' && bytecode[1] == 'X' && bytecode[2] == 'B' && bytecode[3] == 'C',
                $"{file} {entry} {profile} is not shader bytecode.");
        }
    }

    [Fact]
    public void MissingShader_SaysWhatToAdd()
    {
        var error = Assert.Throws<InvalidOperationException>(() => ShaderSource.Load("Magnify.hlsl", "Missing", "ps_4_0"));

        Assert.Contains("ShaderEntry", error.Message, StringComparison.Ordinal);
    }

    private static IEnumerable<string> RenderingSources()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "ZoomIN.slnx")))
        {
            folder = folder.Parent;
        }

        var src = Path.Combine(folder!.FullName, "src", "ZoomIN.Rendering");
        return Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(File.ReadAllText);
    }

    [GeneratedRegex(@"ShaderSource\.Load\(""(?<file>[^""]+)"",\s*""(?<entry>[^""]+)"",\s*""(?<profile>[^""]+)""\)")]
    private static partial Regex LoadCall();
}
