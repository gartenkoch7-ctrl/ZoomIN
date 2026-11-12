using System.Globalization;
using System.Text.RegularExpressions;
using ZoomIN.Core.Filters;
using ZoomIN.Core.WindowFilters;

namespace ZoomIN.Rendering.Tests.Gpu;

/// <summary>
/// The #defines of Magnify.hlsl that size the palette and the replacement block (M18) must match
/// the C# side, or the shader reads replacements from the wrong place.
/// </summary>
public sealed partial class SchemeShaderConstantsTests
{
    [Theory]
    [InlineData("PALETTE_SIZE", WindowPalette.Count)]
    [InlineData("FIRST_SCHEME_ENTRY", WindowPalette.FirstScheme)]
    [InlineData("MAX_SCHEMES", CustomSchemes.Max)]
    [InlineData("REPLACEMENTS_PER_SCHEME", CustomScheme.MaxReplacements)]
    public void ShaderDefine_MatchesTheCSharpConstant(string name, int expected)
    {
        var match = Define().Matches(File.ReadAllText(ShaderPath())).FirstOrDefault(m => m.Groups["name"].Value == name);

        Assert.NotNull(match);
        Assert.Equal(expected, int.Parse(match.Groups["value"].Value, CultureInfo.InvariantCulture));
    }

    private static string ShaderPath()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "ZoomIN.slnx")))
        {
            folder = folder.Parent;
        }

        Assert.NotNull(folder);
        return Path.Combine(folder.FullName, "src", "ZoomIN.Rendering", "Shaders", "Magnify.hlsl");
    }

    [GeneratedRegex(@"^#define\s+(?<name>\w+)\s+(?<value>\d+)", RegexOptions.Multiline)]
    private static partial Regex Define();
}
