using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ZoomIN.ShaderCompiler;

/// <summary>
/// Inlines <c>#include "File.hlsli"</c> lines from the shader folder before compiling, so the
/// shaders can be split into files of their own (300-line rule) without an include handler. Each
/// inlined part is framed by #line directives: compiler messages name the file and line they come
/// from. Only quoted includes next to the shader; each file once (a cycle or repeat is an error).
/// </summary>
internal static partial class IncludeExpander
{
    public static string Expand(string folder, string file) => Expand(folder, file, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

    private static string Expand(string folder, string file, HashSet<string> seen)
    {
        if (!seen.Add(file))
        {
            throw new InvalidOperationException($"'{file}' is included more than once.");
        }

        var lines = File.ReadAllLines(Path.Combine(folder, file));
        var result = new StringBuilder();
        for (var i = 0; i < lines.Length; i++)
        {
            var match = Include().Match(lines[i]);
            if (!match.Success)
            {
                result.AppendLine(lines[i]);
                continue;
            }

            var included = match.Groups[1].Value;
            if (Path.GetFileName(included) != included)
            {
                throw new InvalidOperationException($"{file}({i + 1}): only files next to the shader can be included, not '{included}'.");
            }

            result.AppendLine(CultureInfo.InvariantCulture, $"#line 1 \"{included}\"");
            result.Append(Expand(folder, included, seen)); // ends with a line break already
            result.AppendLine(CultureInfo.InvariantCulture, $"#line {i + 2} \"{file}\"");
        }

        return result.ToString();
    }

    [GeneratedRegex("""^\s*#include\s+"([^"]+)"\s*$""")]
    private static partial Regex Include();
}
