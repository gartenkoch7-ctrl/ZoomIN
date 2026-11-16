using System.Globalization;

namespace ZoomIN.Core.Updates;

/// <summary>
/// A semantic version as used in release tags (v1.0.0, v1.0.0-rc1, v1.1.0-dev.42). Ordered by
/// SemVer 2.0 precedence, so 1.1.0-dev.9 &lt; 1.1.0-dev.10 &lt; 1.1.0-rc1 &lt; 1.1.0.
/// Build metadata (+abc) is ignored.
/// </summary>
public sealed record AppVersion(int Major, int Minor, int Patch, string PreRelease = "") : IComparable<AppVersion>
{
    public bool IsPreRelease => PreRelease.Length > 0;

    public static bool TryParse(string? text, out AppVersion version)
    {
        version = new AppVersion(0, 0, 0);
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var core = text.Trim().TrimStart('v', 'V');
        var plus = core.IndexOf('+', StringComparison.Ordinal);
        if (plus >= 0)
        {
            core = core[..plus];
        }

        var preRelease = string.Empty;
        var dash = core.IndexOf('-', StringComparison.Ordinal);
        if (dash >= 0)
        {
            preRelease = core[(dash + 1)..];
            core = core[..dash];
            if (preRelease.Length == 0 || preRelease.Split('.').Any(part => part.Length == 0))
            {
                return false;
            }
        }

        var parts = core.Split('.');
        if (parts.Length != 3 || !TryNumber(parts[0], out var major) || !TryNumber(parts[1], out var minor) ||
            !TryNumber(parts[2], out var patch))
        {
            return false;
        }

        version = new AppVersion(major, minor, patch, preRelease);
        return true;
    }

    public int CompareTo(AppVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var result = Major.CompareTo(other.Major);
        result = result != 0 ? result : Minor.CompareTo(other.Minor);
        result = result != 0 ? result : Patch.CompareTo(other.Patch);
        return result != 0 ? result : ComparePreRelease(PreRelease, other.PreRelease);
    }

    public static bool operator <(AppVersion left, AppVersion right) => left.CompareTo(right) < 0;

    public static bool operator >(AppVersion left, AppVersion right) => left.CompareTo(right) > 0;

    public static bool operator <=(AppVersion left, AppVersion right) => left.CompareTo(right) <= 0;

    public static bool operator >=(AppVersion left, AppVersion right) => left.CompareTo(right) >= 0;

    public override string ToString() =>
        IsPreRelease ? $"{Major}.{Minor}.{Patch}-{PreRelease}" : $"{Major}.{Minor}.{Patch}";

    private static bool TryNumber(string text, out int value) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);

    // A release outranks its pre-releases; identifiers compare numerically when both are numbers,
    // numbers rank below text, and a shorter list of equal identifiers ranks lower (SemVer 11.4).
    private static int ComparePreRelease(string left, string right)
    {
        if (left.Length == 0 || right.Length == 0)
        {
            return (left.Length == 0, right.Length == 0) switch
            {
                (true, true) => 0,
                (true, false) => 1,
                _ => -1,
            };
        }

        var a = left.Split('.');
        var b = right.Split('.');
        for (var i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            var result = CompareIdentifier(a[i], b[i]);
            if (result != 0)
            {
                return result;
            }
        }

        return a.Length.CompareTo(b.Length);
    }

    private static int CompareIdentifier(string left, string right)
    {
        var leftIsNumber = TryNumber(left, out var x);
        var rightIsNumber = TryNumber(right, out var y);
        return (leftIsNumber, rightIsNumber) switch
        {
            (true, true) => x.CompareTo(y),
            (true, false) => -1,
            (false, true) => 1,
            _ => string.CompareOrdinal(left, right),
        };
    }
}
