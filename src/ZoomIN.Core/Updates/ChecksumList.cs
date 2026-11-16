using System.Globalization;

namespace ZoomIN.Core.Updates;

/// <summary>
/// Reads <see cref="ReleaseAssets.ChecksumsName"/> in the format of <c>sha256sum</c>:
/// one "&lt;64 hex digits&gt;  &lt;file name&gt;" per line ("*" before the name marks binary mode).
/// </summary>
public static class ChecksumList
{
    private const int Sha256Length = 32;

    public static bool TryFind(string content, string fileName, out byte[] sha256)
    {
        ArgumentNullException.ThrowIfNull(content);
        sha256 = [];
        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.Trim();
            var space = line.IndexOf(' ', StringComparison.Ordinal);
            if (space < 0)
            {
                continue;
            }

            var name = line[(space + 1)..].TrimStart().TrimStart('*');
            if (string.Equals(name, fileName, StringComparison.Ordinal) && TryParseHex(line[..space], out sha256))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryParseHex(string hex, out byte[] bytes)
    {
        bytes = new byte[Sha256Length];
        if (hex.Length != Sha256Length * 2)
        {
            return false;
        }

        for (var i = 0; i < Sha256Length; i++)
        {
            if (!byte.TryParse(hex.AsSpan(i * 2, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out bytes[i]))
            {
                return false;
            }
        }

        return true;
    }
}
