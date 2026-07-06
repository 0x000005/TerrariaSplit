using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal sealed class SettingsPageHost
{
    private readonly SettingsForm owner;
    private readonly AppSettings draft;
    private readonly SettingsUiFactory factory;
    private readonly SettingsDialogService dialogs;
    private readonly ISettingsSnapshotFactory settingsSnapshots;
    private readonly Func<RuntimePerformanceDiagnostics> runtimeDiagnosticsProvider;
    private readonly Func<RuntimeDebugSnapshot> runtimeDebugSnapshotProvider;
    private readonly Panel pageHost;
    private readonly List<PageEntry> pages = new();
    private SettingsPageId? selectedPageId;

    public SettingsPageHost(
        SettingsForm owner,
        AppSettings draft,
        SettingsUiFactory factory,
        SettingsDialogService dialogs,
        ISettingsSnapshotFactory settingsSnapshots,
        Func<RuntimePerformanceDiagnostics> runtimeDiagnosticsProvider,
        Func<RuntimeDebugSnapshot> runtimeDebugSnapshotProvider,
        Panel pageHost)
    {
        this.owner = owner;
        this.draft = draft;
        this.factory = factory;
        this.dialogs = dialogs;
        this.settingsSnapshots = settingsSnapshots;
        this.runtimeDiagnosticsProvider = runtimeDiagnosticsProvider;
        this.runtimeDebugSnapshotProvider = runtimeDebugSnapshotProvider;
        this.pageHost = pageHost;
    }

    public IReadOnlyList<PageEntry> Pages => pages;

    public void Register(string title, ISettingsPage page)
    {
        pages.Add(new PageEntry(
            page.Id,
            title,
            CreateNavButton(title),
            page));
    }

    public void AttachNavigation(FlowLayoutPanel navPanel)
    {
        navPanel.SuspendLayout();
        try
        {
            navPanel.Controls.Clear();
            foreach (PageEntry page in pages)
            {
                page.Nav.Click += (_, _) => Select(page.Id);
                navPanel.Controls.Add(page.Nav);
            }
        }
        finally
        {
            navPanel.ResumeLayout(true);
        }
    }

    public void Select(SettingsPageId id)
    {
        if (selectedPageId == id)
        {
            return;
        }

        if (selectedPageId.HasValue &&
            TryGetCreatedPage(selectedPageId.Value, out ISettingsPage? previousPage))
        {
            if (previousPage is ISettingsPageLifecycle previousLifecycle)
            {
                previousLifecycle.OnDeselected();
            }
        }

        PageEntry entry = GetEntry(id);
        Control pageControl = EnsurePageCreated(entry);

        foreach (PageEntry page in pages)
        {
            bool selected = page.Id == id;
            if (page.PageControl is not null)
            {
                page.PageControl.Visible = selected;
            }

            page.Nav.BackColor = selected ? UiTheme.Accent : UiTheme.SurfaceRaised;
            page.Nav.FlatAppearance.BorderColor = selected ? UiTheme.Accent : UiTheme.Border;
        }

        if (entry.PageDefinition is ISettingsPageLifecycle lifecycle)
        {
            lifecycle.OnSelected();
        }

        pageControl.Visible = true;
        pageControl.BringToFront();
        selectedPageId = id;
    }

    public bool IsCreated(SettingsPageId id)
    {
        return pages.Any(page => page.Id == id && page.PageControl is not null);
    }

    public T GetOrCreatePage<T>(SettingsPageId id) where T : class, ISettingsPage
    {
        PageEntry entry = GetEntry(id);
        EnsurePageCreated(entry);
        return (T)entry.PageDefinition;
    }

    public void ApplyToSettings()
    {
        ApplyTo(draft);
    }

    public AppSettings CreateAppliedSnapshot()
    {
        AppSettings snapshot = settingsSnapshots.CreateSnapshot(draft);
        ApplyTo(snapshot);
        return snapshot;
    }

    private void ApplyTo(AppSettings target)
    {
        ApplyIfCreated(SettingsPageId.General, target);
        ApplyIfCreated(SettingsPageId.Automation, target);
        ApplyIfCreated(SettingsPageId.Data, target);
        ApplyIfCreated(SettingsPageId.Splits, target);
        SettingsNormalizer.Normalize(target);
        ApplyIfCreated(SettingsPageId.Effects, target);
        SettingsNormalizer.Normalize(target);
        ApplyIfCreated(SettingsPageId.Ui, target);
        ApplyIfCreated(SettingsPageId.Advanced, target);
        ApplyIfCreated(SettingsPageId.Colors, target);
        ApplyIfCreated(SettingsPageId.Sounds, target);
        ApplyIfCreated(SettingsPageId.Debug, target);
    }

    public void NotifyModelChanged(SettingsModelChange change)
    {
        foreach (PageEntry page in pages)
        {
            if (page.PageControl is null)
            {
                continue;
            }

            if (page.PageDefinition is ISettingsModelListener listener)
            {
                listener.OnModelChanged(change);
            }
        }
    }

    private void ApplyIfCreated(SettingsPageId id, AppSettings target)
    {
        PageEntry entry = GetEntry(id);
        if (entry.PageControl is not null)
        {
            entry.PageDefinition.Apply(target);
        }
    }

    private bool TryGetCreatedPage(SettingsPageId id, out ISettingsPage? page)
    {
        PageEntry entry = GetEntry(id);
        page = entry.PageControl is null ? null : entry.PageDefinition;
        return page is not null;
    }

    private PageEntry GetEntry(SettingsPageId id)
    {
        return pages.First(page => page.Id == id);
    }

    private Control EnsurePageCreated(PageEntry entry)
    {
        if (entry.PageControl is not null)
        {
            return entry.PageControl;
        }

        pageHost.SuspendLayout();
        try
        {
            var context = new SettingsPageContext(
                owner,
                draft,
                factory,
                dialogs,
                runtimeDiagnosticsProvider,
                runtimeDebugSnapshotProvider,
                NotifyModelChanged);
            Control page = entry.PageDefinition.Build(context);
            page.Dock = DockStyle.Fill;
            page.Visible = false;
            entry.PageControl = page;
            pageHost.Controls.Add(page);
            return page;
        }
        finally
        {
            pageHost.ResumeLayout(true);
        }
    }

    private Button CreateNavButton(string text)
    {
        return factory.CreateNavigationButton(text);
    }

    internal sealed class PageEntry
    {
        public PageEntry(SettingsPageId id, string title, Button nav, ISettingsPage pageDefinition)
        {
            Id = id;
            Title = title;
            Nav = nav;
            PageDefinition = pageDefinition;
        }

        public SettingsPageId Id { get; }

        public string Title { get; }

        public Button Nav { get; }

        public ISettingsPage PageDefinition { get; }

        public Control? PageControl { get; set; }
    }
}
