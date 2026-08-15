using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal sealed class AboutSettingsPage : SettingsPageBase
{
    private readonly IApplicationUpdateService updateService;
    private readonly Label versionValue = new();
    private readonly Label statusLabel = new();
    private readonly UiProgressBar progressBar = new();
    private readonly Button updateButton = new();
    private CancellationTokenSource? operationCancellation;
    private long operationGeneration;

    public AboutSettingsPage(IApplicationUpdateService updateService)
    {
        this.updateService = updateService;
    }

    public override SettingsPageId Id => SettingsPageId.About;

    internal string DisplayedVersion => versionValue.Text;

    internal int ProductSectionNaturalHeight { get; private set; }

    internal int ProductSectionMinimumHeight { get; private set; }

    protected override Control BuildPage(SettingsPageContext context)
    {
        Control page = context.BuildScrollPage(BuildSections);
        page.Disposed += (_, _) => CancelOperation();
        return page;
    }

    private void BuildSections(TableLayoutPanel parent)
    {
        var productName = new Label
        {
            AutoSize = true,
            Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
            ForeColor = UiTheme.Text,
            Font = UiTheme.FormFont(23f, FontStyle.Bold),
            Margin = Padding.Empty,
            Text = "Terraria Split"
        };

        versionValue.AutoSize = true;
        versionValue.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
        versionValue.ForeColor = UiTheme.Text;
        versionValue.Font = UiTheme.FormFont(13.5f, FontStyle.Bold);
        versionValue.Margin = new Padding(10, 0, 0, 0);
        versionValue.Text = $"v{updateService.CurrentVersion.ToString(4)}";

        var titleLine = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.None,
            BackColor = Color.Transparent,
            ColumnCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            RowCount = 1
        };
        titleLine.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        titleLine.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        titleLine.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        titleLine.Controls.Add(productName, 0, 0);
        titleLine.Controls.Add(versionValue, 1, 0);

        var titleHost = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            ColumnCount = 3,
            Dock = DockStyle.Top,
            Margin = Padding.Empty,
            Padding = new Padding(0, 4, 0, 4),
            RowCount = 1
        };
        titleHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        titleHost.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        titleHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        titleHost.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        titleHost.Controls.Add(titleLine, 1, 0);

        statusLabel.AutoSize = true;
        statusLabel.Dock = DockStyle.Top;
        statusLabel.ForeColor = UiTheme.MutedText;
        statusLabel.Margin = new Padding(0, 14, 0, 10);
        statusLabel.Text = Context.Localize("Click Check for updates to query GitHub.");

        progressBar.Dock = DockStyle.Top;
        progressBar.Height = 18;
        progressBar.Margin = new Padding(0, 4, 0, 14);
        progressBar.Visible = false;

        updateButton.Text = Context.Localize("Check for updates");
        UiTheme.StyleButton(updateButton, accent: true, minimumWidth: 180);
        updateButton.AutoSize = true;
        updateButton.Click += async (_, _) => await CheckAndUpdateAsync();

        TableLayoutPanel product = Factory.CreateSection();
        SettingsUiFactory.AddSectionControl(product, titleHost);
        ProductSectionNaturalHeight = product.GetPreferredSize(Size.Empty).Height;
        ProductSectionMinimumHeight = ProductSectionNaturalHeight * 2;
        product.MinimumSize = new Size(0, ProductSectionMinimumHeight);
        product.RowStyles[0] = new RowStyle(SizeType.Percent, 100f);
        titleHost.AutoSize = false;
        titleHost.Dock = DockStyle.Fill;
        titleHost.RowStyles[0] = new RowStyle(SizeType.Percent, 100f);
        SettingsUiFactory.AddSection(parent, product);

        TableLayoutPanel update = Factory.CreateSection("Updates");
        SettingsUiFactory.AddSectionControl(update, statusLabel);
        SettingsUiFactory.AddSectionControl(update, progressBar);
        SettingsUiFactory.AddSectionControl(update, Factory.CreateButtonPanel(updateButton));
        SettingsUiFactory.AddSection(parent, update);
    }

    private async Task CheckAndUpdateAsync()
    {
        CancelOperation();
        operationCancellation = new CancellationTokenSource();
        CancellationToken token = operationCancellation.Token;
        long generation = ++operationGeneration;
        SetBusy(Context.Localize("Checking for updates..."), showProgress: false);
        try
        {
            ApplicationUpdateCheckResult result = await updateService.CheckAsync(token);
            if (result.Kind == ApplicationUpdateCheckKind.UpToDate || result.Release is null)
            {
                SetIdle(Context.Localize("TerrariaSplit is up to date."));
                return;
            }

            ApplicationUpdateRelease release = result.Release;
            statusLabel.Text = string.Format(
                Context.Localize("Version {0} is available."),
                release.Version.ToString(4));
            bool confirmed = Dialogs.Confirm(
                string.Format(
                    Context.Localize("Current version: {0}\nNew version: {1}\n\nDownload, install, and restart TerrariaSplit now?"),
                    result.CurrentVersion.ToString(4),
                    release.Version.ToString(4)),
                Context.Localize("TerrariaSplit Update"));
            if (!confirmed)
            {
                SetIdle(Context.Localize("Update cancelled."));
                return;
            }

            SetBusy(Context.Localize("Downloading update..."), showProgress: true);
            var progress = new Progress<ApplicationUpdateProgress>(progress => ReportProgress(generation, progress));
            PreparedApplicationUpdate prepared = await updateService.PrepareAsync(release, progress, token);
            statusLabel.Text = Context.Localize("Update verified. Preparing to restart...");
            progressBar.Value = 0;
            if (!Owner.RequestApplicationUpdate(prepared))
            {
                prepared.Discard();
                SetIdle(Context.Localize("Update could not be started."));
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            if (!Owner.IsDisposed)
            {
                SetIdle(Context.Localize("Update cancelled."));
            }
        }
        catch (Exception ex)
        {
            FileAppLogger.Instance.Error(ex, "Application update failed.");
            SetIdle(string.Format(Context.Localize("Update failed: {0}"), ex.Message));
        }
        finally
        {
            if (operationGeneration == generation)
            {
                operationGeneration++;
            }
        }
    }

    private void ReportProgress(long generation, ApplicationUpdateProgress progress)
    {
        if (operationGeneration != generation)
        {
            return;
        }

        if (progress.Verifying)
        {
            progressBar.Value = 0;
            statusLabel.Text = Context.Localize("Verifying update...");
            return;
        }

        if (progress.TotalBytes is > 0)
        {
            progressBar.Value = (int)Math.Clamp(progress.BytesReceived * 100 / progress.TotalBytes.Value, 0, 100);
            statusLabel.Text = string.Format(
                Context.Localize("Downloading update... {0}%"),
                progressBar.Value);
        }
        else
        {
            progressBar.Value = 0;
        }
    }

    private void SetBusy(string status, bool showProgress)
    {
        updateButton.Enabled = false;
        statusLabel.Text = status;
        progressBar.Visible = showProgress;
        progressBar.Value = 0;
    }

    private void SetIdle(string status)
    {
        updateButton.Enabled = true;
        statusLabel.Text = status;
        progressBar.Visible = false;
        progressBar.Value = 0;
    }

    private void CancelOperation()
    {
        operationCancellation?.Cancel();
        operationCancellation?.Dispose();
        operationCancellation = null;
    }
}
