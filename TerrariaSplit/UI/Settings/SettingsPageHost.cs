using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class SettingsPageHost
{
    private readonly SettingsForm owner;
    private readonly AppSettings draft;
    private readonly SettingsUiFactory factory;
    private readonly SettingsDialogService dialogs;
    private readonly Func<RuntimePerformanceDiagnostics> runtimeDiagnosticsProvider;
    private readonly Panel pageHost;
    private readonly List<PageEntry> pages = new();
    private SettingsPageId? selectedPageId;

    public SettingsPageHost(
        SettingsForm owner,
        AppSettings draft,
        SettingsUiFactory factory,
        SettingsDialogService dialogs,
        Func<RuntimePerformanceDiagnostics> runtimeDiagnosticsProvider,
        Panel pageHost)
    {
        this.owner = owner;
        this.draft = draft;
        this.factory = factory;
        this.dialogs = dialogs;
        this.runtimeDiagnosticsProvider = runtimeDiagnosticsProvider;
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
        ApplyIfCreated(SettingsPageId.General);
        ApplyIfCreated(SettingsPageId.Automation);
        ApplyIfCreated(SettingsPageId.Data);
        ApplyIfCreated(SettingsPageId.Boss);
        AppSettingsStore.Normalize(draft);
        ApplyIfCreated(SettingsPageId.Effects);
        AppSettingsStore.Normalize(draft);
        ApplyIfCreated(SettingsPageId.Ui);
        ApplyIfCreated(SettingsPageId.Advanced);
        ApplyIfCreated(SettingsPageId.Colors);
        ApplyIfCreated(SettingsPageId.Sounds);
        ApplyIfCreated(SettingsPageId.Debug);
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

    private void ApplyIfCreated(SettingsPageId id)
    {
        PageEntry entry = GetEntry(id);
        if (entry.PageControl is not null)
        {
            entry.PageDefinition.Apply(draft);
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
        var button = new Button
        {
            Text = owner.Localize(text),
            Width = 148,
            Height = 46,
            Margin = new Padding(0, 0, 0, 8),
            TextAlign = ContentAlignment.MiddleLeft
        };
        UiTheme.StyleButton(button, accent: false, minimumWidth: 148);
        button.Height = 46;
        button.MinimumSize = new Size(148, 46);
        button.Padding = new Padding(14, 0, 14, 2);
        return button;
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
