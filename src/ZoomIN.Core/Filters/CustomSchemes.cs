using ZoomIN.Core.Pointer;

namespace ZoomIN.Core.Filters;

/// <summary>Places, lookup and clean-up of the own schemes (M18, C1–C3). Pure logic, no state.</summary>
public static class CustomSchemes
{
    /// <summary>The most own schemes (decision: 4).</summary>
    public const int Max = 4;

    /// <summary>What a filter falls back to when its own scheme is gone (decision: "Inverted").</summary>
    public const FilterId Fallback = FilterId.Inverted;

    public static IReadOnlyList<CustomScheme> None { get; } = [];

    public static bool IsCustom(FilterId id) => id is >= FilterId.Custom1 and <= FilterId.Custom4;

    /// <summary>0–3 for an own scheme, −1 for a built-in filter.</summary>
    public static int SlotOf(FilterId id) => IsCustom(id) ? id - FilterId.Custom1 : -1;

    public static FilterId IdOf(int slot) => FilterId.Custom1 + Math.Clamp(slot, 0, Max - 1);

    /// <summary>The scheme in place <paramref name="id"/>, or null. No allocation.</summary>
    public static CustomScheme? Find(IReadOnlyList<CustomScheme> schemes, FilterId id)
    {
        for (var i = 0; i < schemes.Count; i++)
        {
            if (schemes[i].Id == id)
            {
                return schemes[i];
            }
        }

        return null;
    }

    /// <summary>
    /// The filter that really shows: an own scheme without a definition (deleted, or an unknown
    /// value from a hand-edited file) shows <see cref="Fallback"/>.
    /// </summary>
    public static FilterId Resolve(FilterId id, IReadOnlyList<CustomScheme> schemes) =>
        !Enum.IsDefined(id) || (IsCustom(id) && Find(schemes, id) is null) ? Fallback : id;

    /// <summary>The first free place, or null if all <see cref="Max"/> are taken.</summary>
    public static FilterId? FreeSlot(IReadOnlyList<CustomScheme> schemes)
    {
        for (var slot = 0; slot < Max; slot++)
        {
            if (Find(schemes, IdOf(slot)) is null)
            {
                return IdOf(slot);
            }
        }

        return null;
    }

    /// <summary>
    /// Drops broken entries (no own place, duplicate place) and bounds every value: names, kind,
    /// colours, strength, at most <see cref="CustomScheme.MaxReplacements"/> replacements. Sorted by
    /// place. Used on load, for the secure desktop and after every edit.
    /// </summary>
    public static IReadOnlyList<CustomScheme> Normalized(IReadOnlyList<CustomScheme>? schemes)
    {
        var result = new List<CustomScheme>(Max);
        foreach (var scheme in schemes ?? None)
        {
            if (scheme is null || !IsCustom(scheme.Id) || Find(result, scheme.Id) is not null)
            {
                continue;
            }

            result.Add(NormalizedScheme(scheme));
        }

        result.Sort((a, b) => a.Id.CompareTo(b.Id));
        return result;
    }

    public static CustomScheme NormalizedScheme(CustomScheme scheme)
    {
        ArgumentNullException.ThrowIfNull(scheme);
        var d = new CustomScheme();
        return scheme with
        {
            Name = Name(scheme.Name),

            // An unknown kind given as a number (hand edit; an unknown name already fails the whole file
            // in the string enum converter) keeps the scheme and its colours: it only
            // replaces colours until the user picks a kind, instead of losing the definition.
            Kind = Enum.IsDefined(scheme.Kind) ? scheme.Kind : SchemeKind.ReplaceOnly,
            Color1 = Hex(scheme.Color1, d.Color1),
            Color2 = Hex(scheme.Color2, d.Color2),
            Hue = Hex(scheme.Hue, d.Hue),
            Deficiency = Enum.IsDefined(scheme.Deficiency) ? scheme.Deficiency : d.Deficiency,
            Strength = Math.Clamp(scheme.Strength, 0, CustomScheme.MaxStrength),
            Replacements = [.. (scheme.Replacements ?? []).Where(r => r is not null).Take(CustomScheme.MaxReplacements).Select(Normalized)],
        };
    }

    private static ColorReplacement Normalized(ColorReplacement replacement)
    {
        var d = new ColorReplacement();
        return replacement with
        {
            From = Hex(replacement.From, d.From),
            To = Hex(replacement.To, d.To),
            Tolerance = Math.Clamp(replacement.Tolerance, 0, ColorReplacement.MaxTolerance),
        };
    }

    // Trimmed and cut to the limit, never between the two halves of a surrogate pair (emoji).
    private static string Name(string? value)
    {
        var name = (value ?? string.Empty).Trim();
        if (name.Length <= CustomScheme.MaxNameLength)
        {
            return name;
        }

        var length = char.IsHighSurrogate(name[CustomScheme.MaxNameLength - 1]) ? CustomScheme.MaxNameLength - 1 : CustomScheme.MaxNameLength;
        return name[..length];
    }

    // Only "#RRGGBB" survives, like the pointer colours.
    private static string Hex(string? value, string fallback) => RgbColor.NormalizedHex(value, fallback);
}
