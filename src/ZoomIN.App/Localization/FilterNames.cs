using ZoomIN.Core.Filters;
using ZoomIN.Core.Localization;

namespace ZoomIN.App.Localization;

/// <summary>
/// Display names of filters for every list and message (M18): built-in filters from the text tables,
/// own schemes by the name the user gave them, or "Own scheme N" while it has none.
/// </summary>
public sealed class FilterNames(ILocalizer texts)
{
    /// <summary>What <paramref name="filter"/> shows: a place without a scheme is named "Inverted", like it looks.</summary>
    public string Of(FilterId filter, IReadOnlyList<CustomScheme> schemes)
    {
        var shown = CustomSchemes.Resolve(filter, schemes);
        if (!CustomSchemes.IsCustom(shown))
        {
            return texts.Get("Filter." + shown);
        }

        var name = CustomSchemes.Find(schemes, shown)!.Name;
        return string.IsNullOrWhiteSpace(name) ? DefaultName(shown) : name;
    }

    /// <summary>The names of <paramref name="order"/>, in that order.</summary>
    public IReadOnlyList<string> Of(IReadOnlyList<FilterId> order, IReadOnlyList<CustomScheme> schemes) =>
        [.. order.Select(f => Of(f, schemes))];

    /// <summary>"Own scheme 2" for place Custom2.</summary>
    public string DefaultName(FilterId filter) => texts.Format("Schemes.DefaultName", CustomSchemes.SlotOf(filter) + 1);
}
