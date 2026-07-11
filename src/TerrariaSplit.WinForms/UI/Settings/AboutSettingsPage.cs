using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal sealed class AboutSettingsPage : SettingsPageBase
{
    private readonly IApplicationUpdateService updateService;
    private readonly Label versionValue = new();
    private readonly Label statusLabel = new();
    private readonly ProgressBar progressBar = new();
    private readonly Button updateButton = new();
    private CancellationTokenSource? operationCancellation;

    public AboutSettingsPage(IApplicationUpdateService updateService)
    {
        this.updateService = updateService;
    }

    public override SettingsPageId Id => SettingsPageId.About;

    internal string DisplayedVersion => versionValue.Text;

    protected override Control BuildPage(SettingsPageContext context)
    {
        Control page = context.BuildScrollPage(BuildSections);
        page.Disposed += (_, _) => CancelOperation();
        return page;
    }

    private void BuildSections(TableLayoutPanel parent)
    {
        versionValue.AutoSize = true;
        versionValue.ForeColor = UiTheme.Text;
        versionValue.Font = UiTheme.FormFont(11f, FontStyle.Bold);
        versionValue.Text = updateService.CurrentVersion.ToString(4);

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

        TableLayoutPanel product = Factory.CreateSection("About TerrariaSplit");
        TableLayoutPanel grid = Factory.CreateTwoColumnGrid(280f);
        Factory.AddSettingRow(grid, "Application", Factory.CreateRawRowLabel("TerrariaSplit"));
        Factory.AddSettingRow(grid, "Version", versionValue);
        SettingsUiFactory.AddSectionControl(product, grid);
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
            var progress = new Progress<ApplicationUpdateProgress>(ReportProgress);
            PreparedApplicationUpdate prepared = await updateService.PrepareAsync(release, progress, token);
            statusLabel.Text = Context.Localize("Update verified. Preparing to restart...");
            progressBar.Style = ProgressBarStyle.Marquee;
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
            StaticAppLogger.Instance.Error(ex, "Application update failed.");
            SetIdle(string.Format(Context.Localize("Update failed: {0}"), ex.Message));
        }
    }

    private void ReportProgress(ApplicationUpdateProgress progress)
    {
        if (progress.Verifying)
        {
            progressBar.Style = ProgressBarStyle.Marquee;
            statusLabel.Text = Context.Localize("Verifying update...");
            return;
        }

        if (progress.TotalBytes is > 0)
        {
            progressBar.Style = ProgressBarStyle.Continuous;
            progressBar.Maximum = 1000;
            progressBar.Value = (int)Math.Clamp(progress.BytesReceived * 1000 / progress.TotalBytes.Value, 0, 1000);
            statusLabel.Text = string.Format(
                Context.Localize("Downloading update... {0}%"),
                progressBar.Value / 10);
        }
        else
        {
            progressBar.Style = ProgressBarStyle.Marquee;
        }
    }

    private void SetBusy(string status, bool showProgress)
    {
        updateButton.Enabled = false;
        statusLabel.Text = status;
        progressBar.Visible = showProgress;
        progressBar.Style = ProgressBarStyle.Marquee;
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
