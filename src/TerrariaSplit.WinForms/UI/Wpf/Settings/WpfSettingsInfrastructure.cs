using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TerrariaSplit.UI.Wpf.Dialogs;
using TerrariaSplit.UI.Wpf.Interop;
using FormsColorDialog = System.Windows.Forms.ColorDialog;
using FormsDialogResult = System.Windows.Forms.DialogResult;
using FormsMessageBoxButtons = System.Windows.Forms.MessageBoxButtons;
using FormsMessageBoxIcon = System.Windows.Forms.MessageBoxIcon;
using FormsOpenFileDialog = System.Windows.Forms.OpenFileDialog;
using FormsWin32Window = System.Windows.Forms.IWin32Window;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfRadioButton = System.Windows.Controls.RadioButton;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace TerrariaSplit.UI.Wpf.Settings;

internal interface IWpfSettingsWindowOwner
{
    Window Window { get; }

    bool IsDisposed { get; }

    string Localize(string key);

    bool RequestApplicationUpdate(PreparedApplicationUpdate update);
}

internal interface IWpfSettingsPage
{
    SettingsPageId Id { get; }

    FrameworkElement Build(WpfSettingsPageContext context);

    void Apply(AppSettings settings);
}

internal interface IWpfSettingsPageLifecycle
{
    void OnSelected();

    void OnDeselected();

    void OnDisposing();
}

internal interface IWpfSettingsModelListener
{
    void OnModelChanged(SettingsModelChange change);
}

internal abstract class WpfSettingsPageBase : IWpfSettingsPage, IWpfSettingsPageLifecycle, IWpfSettingsModelListener
{
    protected WpfSettingsPageContext Context { get; private set; } = null!;

    protected IWpfSettingsWindowOwner Owner => Context.Owner;

    protected AppSettings Draft => Context.Draft;

    protected WpfSettingsDialogService Dialogs => Context.Dialogs;

    public abstract SettingsPageId Id { get; }

    public FrameworkElement Build(WpfSettingsPageContext context)
    {
        Context = context;
        return BuildPage(context);
    }

    public virtual void Apply(AppSettings settings)
    {
    }

    public virtual void OnSelected()
    {
    }

    public virtual void OnDeselected()
    {
    }

    public virtual void OnDisposing()
    {
    }

    public virtual void OnModelChanged(SettingsModelChange change)
    {
    }

    protected abstract FrameworkElement BuildPage(WpfSettingsPageContext context);

    protected string Localize(string key) => Context.Localize(key);
}

internal sealed class WpfSettingsPageContext
{
    private readonly Action<SettingsModelChange> notifyModelChanged;

    public WpfSettingsPageContext(
        IWpfSettingsWindowOwner owner,
        AppSettings draft,
        WpfSettingsDialogService dialogs,
        Action<SettingsModelChange> notifyModelChanged)
    {
        Owner = owner;
        Draft = draft;
        Dialogs = dialogs;
        this.notifyModelChanged = notifyModelChanged;
    }

    public IWpfSettingsWindowOwner Owner { get; }

    public AppSettings Draft { get; }

    public WpfSettingsDialogService Dialogs { get; }

    public string Localize(string key) => Owner.Localize(key);

    public void NotifyModelChanged(SettingsModelChange change) => notifyModelChanged(change);
}

internal sealed class WpfSettingsDialogService
{
    private readonly Window owner;
    private readonly Func<string, string> localize;
    private readonly SettingsMessageBoxPresenter messageBoxPresenter;

    public WpfSettingsDialogService(
        Window owner,
        Func<string, string> localize,
        SettingsMessageBoxPresenter? messageBoxPresenter = null)
    {
        this.owner = owner;
        this.localize = localize;
        this.messageBoxPresenter = messageBoxPresenter ?? ShowModernMessage;
    }

    private FormsWin32Window OwnerWin32 => WpfWindowInterop.AsWin32Window(owner);

    public bool PickColor(WpfTextBox target)
    {
        System.Drawing.Color currentColor = ColorText.Parse(target.Text, System.Drawing.Color.White);
        if (currentColor.A == 0)
        {
            currentColor = System.Drawing.Color.White;
        }

        using var dialog = new FormsColorDialog
        {
            Color = currentColor,
            FullOpen = true
        };

        if (dialog.ShowDialog(OwnerWin32) != FormsDialogResult.OK)
        {
            return false;
        }

        target.Text = ColorText.Format(dialog.Color);
        return true;
    }

    public bool PickBossIcon(WpfTextBox target)
    {
        return PickFile(
            target,
            "Choose icon",
            "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*");
    }

    public bool PickSound(WpfTextBox target)
    {
        return PickFile(
            target,
            "Choose sound",
            "Wave audio|*.wav|All files|*.*");
    }

    public bool PickFile(WpfTextBox target, string title, string filter)
    {
        using var dialog = new FormsOpenFileDialog
        {
            CheckFileExists = true,
            Filter = filter,
            Title = localize(title)
        };

        if (dialog.ShowDialog(OwnerWin32) != FormsDialogResult.OK)
        {
            return false;
        }

        target.Text = dialog.FileName;
        return true;
    }

    public void ShowWarning(string message, string title)
    {
        _ = messageBoxPresenter(
            OwnerWin32,
            message,
            title,
            FormsMessageBoxButtons.OK,
            FormsMessageBoxIcon.Warning);
    }

    public bool Confirm(string message, string title)
    {
        return messageBoxPresenter(
            OwnerWin32,
            message,
            title,
            FormsMessageBoxButtons.YesNo,
            FormsMessageBoxIcon.Question) == FormsDialogResult.Yes;
    }

    public void OpenAutoCreateBackupFolder(Func<string, string> localizeTitle)
    {
        try
        {
            string backupRoot = TerrariaSavePaths.DeletedSavesRoot();
            Directory.CreateDirectory(backupRoot);
            Process.Start(new ProcessStartInfo
            {
                FileName = backupRoot,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            AppLog.ForSource().LogError(ex, "Failed to open TerrariaSplit deleted backup folder.");
            ShowWarning(
                localize("Could not open backup folder."),
                localizeTitle("Create World"));
        }
    }

    public void OpenTerrariaSaveFolder(Func<string, string> localizeTitle)
    {
        try
        {
            string saveRoot = TerrariaSavePaths.SaveRoot();
            Directory.CreateDirectory(saveRoot);
            Process.Start(new ProcessStartInfo
            {
                FileName = saveRoot,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            AppLog.ForSource().LogError(ex, "Failed to open Terraria save folder.");
            ShowWarning(
                localize("Could not open save folder."),
                localizeTitle("Create World"));
        }
    }

    private FormsDialogResult ShowModernMessage(
        FormsWin32Window dialogOwner,
        string text,
        string caption,
        FormsMessageBoxButtons buttons,
        FormsMessageBoxIcon icon)
    {
        return ModernMessageDialog.Show(
            dialogOwner,
            caption,
            text,
            buttons,
            icon,
            localize);
    }
}

internal static class WpfSettingsValueParser
{
    public static int ParseIntBox(WpfTextBox textBox, int fallback, int minimum, int maximum)
    {
        return int.TryParse(textBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? Math.Clamp(value, minimum, maximum)
            : fallback;
    }

    public static int ParseTimeBox(WpfTextBox textBox, int fallbackSeconds, int minimumSeconds, int maximumSeconds)
    {
        return TimeText.TryParse(textBox.Text, out TimeSpan value)
            ? Math.Clamp((int)Math.Round(value.TotalSeconds), minimumSeconds, maximumSeconds)
            : fallbackSeconds;
    }

    public static float ParseFloatBox(WpfTextBox textBox, float fallback, float minimum, float maximum)
    {
        return float.TryParse(textBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
            ? Math.Clamp(value, minimum, maximum)
            : fallback;
    }
}

internal static class WpfSettingsBinder
{
    public static void ApplyColors(AppSettings targetSettings, IReadOnlyDictionary<string, WpfTextBox> colorTextBoxes)
    {
        targetSettings.Overlay.Colors ??= new UiColorSettings();

        foreach (TextColorDescriptor descriptor in SettingsDescriptors.TextColors.Concat(SettingsDescriptors.AnimationColors))
        {
            ApplyColor(colorTextBoxes, descriptor.TextKey, value => descriptor.SetText(targetSettings.Overlay.Colors, value));
            ApplyColor(colorTextBoxes, descriptor.OutlineKey, value => descriptor.SetOutline(targetSettings.Overlay.Colors, value));
            ApplyColor(colorTextBoxes, descriptor.ShadowKey, value => descriptor.SetShadow(targetSettings.Overlay.Colors, value));
        }

        foreach (ColorDescriptor descriptor in SettingsDescriptors.IconColors)
        {
            ApplyColor(colorTextBoxes, descriptor.Key, value => descriptor.SetValue(targetSettings.Overlay.Colors, value));
        }
    }

    public static void ApplySounds(AppSettings targetSettings, IReadOnlyDictionary<string, WpfTextBox> soundTextBoxes)
    {
        targetSettings.Overlay.Sounds ??= new UiSoundSettings();

        foreach (SoundDescriptor descriptor in SettingsDescriptors.Sounds)
        {
            if (soundTextBoxes.TryGetValue(descriptor.Key, out WpfTextBox? textBox))
            {
                descriptor.SetValue(targetSettings.Overlay.Sounds, textBox.Text.Trim());
            }
        }
    }

    private static void ApplyColor(
        IReadOnlyDictionary<string, WpfTextBox> colorTextBoxes,
        string key,
        Action<string> setter)
    {
        if (colorTextBoxes.TryGetValue(key, out WpfTextBox? textBox))
        {
            setter(ColorText.Format(ColorText.Parse(textBox.Text, System.Drawing.Color.White)));
        }
    }
}

internal sealed class WpfFontFamilySelector : WpfComboBox
{
    public WpfFontFamilySelector()
    {
        SetResourceReference(StyleProperty, typeof(WpfComboBox));
        foreach (string family in UiFontFactory.Default.GetInstalledFamilyNames())
        {
            Items.Add(family);
        }

        SetSelectedFontFamily(UiFontDefaults.DefaultFamilyName);
    }

    public string SelectedFontFamily => SelectedItem is string family
        ? family
        : UiFontDefaults.DefaultFamilyName;

    public void SetSelectedFontFamily(string familyName)
    {
        string normalized = UiFontFactory.Default.NormalizeFamilyName(familyName);
        SelectedItem = normalized;
        if (SelectedIndex < 0 && Items.Count > 0)
        {
            SelectedIndex = 0;
        }
    }
}

internal static class WpfBitmapBridge
{
    public static BitmapSource FromGdiBitmap(Bitmap bitmap, double dpiX = 96d, double dpiY = 96d)
    {
        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        BitmapData data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        try
        {
            BitmapSource source = BitmapSource.Create(
                bitmap.Width,
                bitmap.Height,
                dpiX > 0 ? dpiX : 96d,
                dpiY > 0 ? dpiY : 96d,
                PixelFormats.Pbgra32,
                null,
                data.Scan0,
                data.Stride * bitmap.Height,
                data.Stride);
            source.Freeze();
            return source;
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }
}

internal sealed class WpfSettingsPageHost
{
    private readonly IWpfSettingsWindowOwner owner;
    private readonly AppSettings draft;
    private readonly WpfSettingsDialogService dialogs;
    private readonly ISettingsSnapshotFactory settingsSnapshots;
    private readonly Grid pageHost;
    private readonly List<PageEntry> pages = new();
    private SettingsPageId? selectedPageId;

    public WpfSettingsPageHost(
        IWpfSettingsWindowOwner owner,
        AppSettings draft,
        WpfSettingsDialogService dialogs,
        ISettingsSnapshotFactory settingsSnapshots,
        Grid pageHost)
    {
        this.owner = owner;
        this.draft = draft;
        this.dialogs = dialogs;
        this.settingsSnapshots = settingsSnapshots;
        this.pageHost = pageHost;
    }

    public IReadOnlyList<PageEntry> Pages => pages;

    public SettingsPageId? SelectedPageId => selectedPageId;

    public void Register(string titleKey, string iconGeometry, IWpfSettingsPage page, bool pinBottom = false)
    {
        WpfRadioButton navButton = WpfSettingsUi.CreateNavRadio(owner.Localize(titleKey), iconGeometry);
        navButton.Checked += (_, _) =>
        {
            if (selectedPageId != page.Id)
            {
                Select(page.Id);
            }
        };
        pages.Add(new PageEntry(page.Id, titleKey, navButton, page, pinBottom));
    }

    public void AttachNavigation(StackPanel primaryNav, StackPanel bottomNav)
    {
        primaryNav.Children.Clear();
        bottomNav.Children.Clear();
        foreach (PageEntry entry in pages)
        {
            if (entry.PinBottom)
            {
                bottomNav.Children.Add(entry.Nav);
            }
            else
            {
                primaryNav.Children.Add(entry.Nav);
            }
        }
    }

    public void Select(SettingsPageId id)
    {
        if (selectedPageId == id)
        {
            return;
        }

        if (selectedPageId.HasValue &&
            TryGetCreatedPage(selectedPageId.Value, out IWpfSettingsPage? previousPage) &&
            previousPage is IWpfSettingsPageLifecycle previousLifecycle)
        {
            previousLifecycle.OnDeselected();
        }

        PageEntry entry = GetEntry(id);
        FrameworkElement pageElement = EnsurePageCreated(entry);

        foreach (PageEntry page in pages)
        {
            bool selected = page.Id == id;
            if (page.PageElement is not null)
            {
                page.PageElement.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
            }

            if (page.Nav.IsChecked != selected)
            {
                page.Nav.IsChecked = selected;
            }
        }

        selectedPageId = id;
        pageElement.Visibility = Visibility.Visible;

        if (entry.PageDefinition is IWpfSettingsPageLifecycle lifecycle)
        {
            lifecycle.OnSelected();
        }
    }

    public bool IsCreated(SettingsPageId id)
    {
        return pages.Any(page => page.Id == id && page.PageElement is not null);
    }

    public T GetOrCreatePage<T>(SettingsPageId id) where T : class, IWpfSettingsPage
    {
        PageEntry entry = GetEntry(id);
        _ = EnsurePageCreated(entry);
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

    public void NotifyModelChanged(SettingsModelChange change)
    {
        foreach (PageEntry page in pages)
        {
            if (page.PageElement is null)
            {
                continue;
            }

            if (page.PageDefinition is IWpfSettingsModelListener listener)
            {
                listener.OnModelChanged(change);
            }
        }
    }

    public void DisposePages()
    {
        foreach (PageEntry page in pages)
        {
            if (page.PageElement is not null &&
                page.PageDefinition is IWpfSettingsPageLifecycle lifecycle)
            {
                lifecycle.OnDisposing();
            }
        }
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
    }

    private void ApplyIfCreated(SettingsPageId id, AppSettings target)
    {
        PageEntry entry = GetEntry(id);
        if (entry.PageElement is not null)
        {
            entry.PageDefinition.Apply(target);
        }
    }

    private bool TryGetCreatedPage(SettingsPageId id, out IWpfSettingsPage? page)
    {
        PageEntry entry = GetEntry(id);
        page = entry.PageElement is null ? null : entry.PageDefinition;
        return page is not null;
    }

    private PageEntry GetEntry(SettingsPageId id)
    {
        return pages.First(page => page.Id == id);
    }

    private FrameworkElement EnsurePageCreated(PageEntry entry)
    {
        if (entry.PageElement is not null)
        {
            return entry.PageElement;
        }

        var context = new WpfSettingsPageContext(owner, draft, dialogs, NotifyModelChanged);
        FrameworkElement element = entry.PageDefinition.Build(context);
        element.Visibility = Visibility.Collapsed;
        entry.PageElement = element;
        pageHost.Children.Add(element);
        return element;
    }

    internal sealed class PageEntry
    {
        public PageEntry(
            SettingsPageId id,
            string title,
            WpfRadioButton nav,
            IWpfSettingsPage pageDefinition,
            bool pinBottom)
        {
            Id = id;
            Title = title;
            Nav = nav;
            PageDefinition = pageDefinition;
            PinBottom = pinBottom;
        }

        public SettingsPageId Id { get; }

        public string Title { get; }

        public WpfRadioButton Nav { get; }

        public IWpfSettingsPage PageDefinition { get; }

        public bool PinBottom { get; }

        public FrameworkElement? PageElement { get; set; }
    }
}
