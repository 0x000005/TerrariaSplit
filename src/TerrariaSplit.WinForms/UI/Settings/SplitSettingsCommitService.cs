namespace TerrariaSplit.UI.Settings;

internal enum SplitCommitMode
{
    StrictApply,
    LenientDeselection
}

internal readonly record struct SplitCommitResult(
    bool Succeeded,
    bool RouteChanged,
    string Message);

internal sealed class SplitSettingsCommitService
{
    private readonly SplitRouteDraft routeDraft;
    private readonly SplitRouteListController routeController;
    private readonly Func<bool> saveSelectedEntry;
    private readonly Func<string> editorErrorProvider;
    private readonly Func<string, string> localize;
    private readonly Action<SettingsModelChange> notifyModelChanged;

    public SplitSettingsCommitService(
        SplitRouteDraft routeDraft,
        SplitRouteListController routeController,
        Func<bool> saveSelectedEntry,
        Func<string> editorErrorProvider,
        Func<string, string> localize,
        Action<SettingsModelChange> notifyModelChanged)
    {
        this.routeDraft = routeDraft;
        this.routeController = routeController;
        this.saveSelectedEntry = saveSelectedEntry;
        this.editorErrorProvider = editorErrorProvider;
        this.localize = localize;
        this.notifyModelChanged = notifyModelChanged;
    }

    public SplitCommitResult CommitTo(AppSettings target, SplitCommitMode mode)
    {
        if (!saveSelectedEntry())
        {
            return new SplitCommitResult(false, false, editorErrorProvider());
        }

        routeDraft.EnsureEntryIds();
        routeDraft.NormalizeAttachedRouteFlags();
        if (mode == SplitCommitMode.LenientDeselection && !routeController.Dirty)
        {
            return new SplitCommitResult(true, false, string.Empty);
        }

        if (!routeDraft.TryValidate(localize, out string validationMessage))
        {
            return new SplitCommitResult(false, false, validationMessage);
        }

        target.Route.SplitRoute = routeDraft.CreateSnapshot();
        SettingsNormalizer.Normalize(target);

        bool routeChanged = routeController.Dirty;
        if (routeChanged)
        {
            notifyModelChanged(SettingsModelChange.RouteChanged);
        }

        routeController.ClearDirty();
        return new SplitCommitResult(true, routeChanged, string.Empty);
    }
}
