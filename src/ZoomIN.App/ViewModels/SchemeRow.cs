using ZoomIN.App.Localization;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Localization;

namespace ZoomIN.App.ViewModels;

/// <summary>One own scheme in the list of the scheme page (M18).</summary>
/// <param name="EditName">What screen readers say for its "Edit" button.</param>
public sealed record SchemeRow(FilterId Id, string Name, string Description, string EditName)
{
    public static SchemeRow Of(CustomScheme scheme, FilterNames names, ILocalizer texts)
    {
        var name = names.Of(scheme.Id, [scheme]);
        return new(scheme.Id, name, texts.Get("Schemes.Kind." + scheme.Kind), texts.Format("Schemes.EditFor", name));
    }
}
