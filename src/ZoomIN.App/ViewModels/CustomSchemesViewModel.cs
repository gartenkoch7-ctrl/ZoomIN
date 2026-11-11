using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ZoomIN.App.Localization;
using ZoomIN.App.Schemes;
using ZoomIN.App.Zoom;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Localization;

namespace ZoomIN.App.ViewModels;

/// <summary>
/// Detail page "Own colour schemes" (M18, C1–C3): the list, new, edit, delete and "show now".
/// Applies live; the settings service saves.
/// </summary>
public sealed partial class CustomSchemesViewModel : ObservableObject
{
    private readonly CustomSchemeService _service;
    private readonly FilterNames _names;
    private readonly ZoomCommands _zoom;
    private readonly ILocalizer _texts;

    public CustomSchemesViewModel(CustomSchemeService service, FilterNames names, ZoomCommands zoom, ILocalizer texts)
    {
        _service = service;
        _names = names;
        _zoom = zoom;
        _texts = texts;
        KindNames = [.. Enum.GetValues<SchemeKind>().Select(k => texts.Get("Schemes.Kind." + k))];
        DeficiencyNames = [.. Enum.GetValues<ColorDeficiency>().Select(d => texts.Get("Schemes.Deficiency." + d))];
        Load();
    }

    public ObservableCollection<SchemeRow> Schemes { get; } = [];

    /// <summary>
    /// One list for every editor, in enum order: a new source per editor would let the combo box
    /// write −1 into the next editor before its own index arrives.
    /// </summary>
    public IReadOnlyList<string> KindNames { get; }

    public IReadOnlyList<string> DeficiencyNames { get; }

    /// <summary>The name box takes no more than the model keeps.</summary>
    public static int MaxNameLength => CustomScheme.MaxNameLength;

    /// <summary>The scheme being edited, or null.</summary>
    [ObservableProperty]
    public partial SchemeEditor? Editor { get; set; }

    /// <summary>Result of the last action, read out by screen readers (live region).</summary>
    [ObservableProperty]
    public partial string Status { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasSchemes { get; set; }

    public bool NoSchemes => !HasSchemes;

    public bool IsEditing => Editor is not null;

    public bool HasStatus => Status.Length > 0;

    /// <summary>A new scheme without a name: lists show "Own scheme N" in the current language until the user names it.</summary>
    public void Add()
    {
        if (_service.Add(string.Empty) is not { } scheme)
        {
            Status = _texts.Get("Schemes.Full");
            return;
        }

        Load();
        Edit(scheme.Id);
        Status = _texts.Format("Schemes.Created", _names.DefaultName(scheme.Id));
    }

    public void Edit(FilterId id)
    {
        Finish();
        Editor = CustomSchemes.Find(_service.Schemes, id) is { } scheme ? new SchemeEditor(scheme, _names, _texts, OnEdited) : null;
    }

    public void ShowEditing()
    {
        if (Editor is { } editor)
        {
            // The monitor the user works on, as the filter page starts with it.
            _service.Show(editor.Id, _zoom.MonitorUnderMouse());
        }
    }

    public void DeleteEditing()
    {
        if (Editor is not { } editor)
        {
            return;
        }

        var name = _names.Of(editor.Id, _service.Schemes);
        var fallbacks = _service.Delete(editor.Id);
        Editor = null;
        Load();
        Status = fallbacks > 0 ? _texts.Format("Schemes.Deleted.Fallback", name, fallbacks) : _texts.Format("Schemes.Deleted", name);
    }

    /// <summary>The page is left or another scheme is picked: the edits of the open one are logged once.</summary>
    public void Finish()
    {
        if (Editor is { } editor)
        {
            _service.Finish(editor.Id);
        }
    }

    partial void OnEditorChanged(SchemeEditor? value) => OnPropertyChanged(nameof(IsEditing));

    partial void OnStatusChanged(string value) => OnPropertyChanged(nameof(HasStatus));

    partial void OnHasSchemesChanged(bool value) => OnPropertyChanged(nameof(NoSchemes));

    private void OnEdited(CustomScheme scheme)
    {
        _service.Update(scheme);
        for (var i = 0; i < Schemes.Count; i++)
        {
            if (Schemes[i].Id == scheme.Id)
            {
                var row = SchemeRow.Of(scheme, _names, _texts);
                if (row != Schemes[i])
                {
                    Schemes[i] = row;
                }
            }
        }
    }

    private void Load()
    {
        Schemes.Clear();
        foreach (var scheme in _service.Schemes)
        {
            Schemes.Add(SchemeRow.Of(scheme, _names, _texts));
        }

        HasSchemes = Schemes.Count > 0;
    }
}
