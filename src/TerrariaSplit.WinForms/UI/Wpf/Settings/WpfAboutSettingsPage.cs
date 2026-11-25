using System.Windows;
using System.Windows.Controls;
using TerrariaSplit.UI.Wpf.Controls;
using WpfButton = System.Windows.Controls.Button;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfProgressBar = System.Windows.Controls.ProgressBar;

namespace TerrariaSplit.UI.Wpf.Settings;

internal sealed class WpfAboutSettingsPage : WpfSettingsPageBase
{
    private readonly IApplicationUpdateService updateService;
    private readonly TextBlock versionValue = new();
    private readonly WpfProgressBar progressBar = new();
    private SettingCard updateCard = null!;
    private WpfButton updateButton = null!;
    private CancellationTokenSource? operationCancellation;
    private long operationGeneration;

    public WpfAboutSettingsPage(IApplicationUpdateService updateService)
    {
        this.updateService = updateService;
    }

    public override SettingsPageId Id => SettingsPageId.About;

    internal string DisplayedVersion => versionValue.Text;

    protected override FrameworkElement BuildPage(WpfSettingsPageContext context)
    {
        return WpfSettingsUi.CreateScrollPage(BuildSections);
    }

    public override void OnDisposing()
    {
        CancelOperation();
    }

    private void BuildSections(StackPanel parent)
    {
        // Product identity card
        var productCard = WpfSettingsUi.CreateCard(
            null,
            out StackPanel productBody,
            contentPadding: new Thickness(18, 32, 18, 32));

        var titleLine = new StackPanel
        {
            Orientation = WpfOrientation.Horizontal,
            HorizontalAlignment = WpfHorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var productName = new TextBlock
        {
            Text = "TerrariaSplit",
            FontSize = 30,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Bottom
        };
        productName.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");

        versionValue.Text = $"v{updateService.CurrentVersion.ToString(4)}";
        versionValue.FontSize = 15;
        versionValue.FontWeight = FontWeights.SemiBold;
        versionValue.Margin = new Thickness(11, 0, 0, 4.5);
        versionValue.VerticalAlignment = VerticalAlignment.Bottom;
        versionValue.SetResourceReference(TextBlock.FontFamilyProperty, "MonoFontFamily");
        versionValue.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");

        titleLine.Children.Add(productName);
        titleLine.Children.Add(versionValue);
        productBody.Children.Add(titleLine);
        parent.Children.Add(productCard);

        // Updates card
        updateCard = WpfSettingsUi.CreateCard(
            Localize("Updates"),
            out StackPanel updateBody,
            contentPadding: new Thickness(14, 10, 14, 12));

        progressBar.Minimum = 0;
        progressBar.Maximum = 100;
        progressBar.Value = 0;
        progressBar.Height = 5;
        progressBar.Margin = new Thickness(0, 0, 0, 10);
        progressBar.Visibility = Visibility.Collapsed;
        updateBody.Children.Add(progressBar);

        updateButton = WpfSettingsUi.CreateButton(Localize("Check for updates"), accent: true, minWidth: 120);
        updateButton.HorizontalAlignment = WpfHorizontalAlignment.Left;
        updateButton.Click += async (_, _) => await CheckAndUpdateAsync();
        updateBody.Children.Add(updateButton);

        parent.Children.Add(updateCard);
    }

    private async Task CheckAndUpdateAsync()
    {
        CancelOperation();
        operationCancellation = new CancellationTokenSource();
        CancellationToken token = operationCancellation.Token;
        long generation = ++operationGeneration;
        SetBusy(Localize("Checking for updates..."), showProgress: false);
        try
        {
            ApplicationUpdateCheckResult result = await updateService.CheckAsync(token);
            if (result.Kind == ApplicationUpdateCheckKind.UpToDate || result.Release is null)
            {
                SetIdle(Localize("TerrariaSplit is up to date."));
                return;
            }

            ApplicationUpdateRelease release = result.Release;
            updateCard.Subtitle = string.Format(
                Localize("Version {0} is available."),
                release.Version.ToString(4));
            bool confirmed = Dialogs.Confirm(
                string.Format(
                    Localize("Current version: {0}\nNew version: {1}\n\nDownload, install, and restart TerrariaSplit now?"),
                    result.CurrentVersion.ToString(4),
                    release.Version.ToString(4)),
                Localize("TerrariaSplit Update"));
            if (!confirmed)
            {
                SetIdle(Localize("Update cancelled."));
                return;
            }

            SetBusy(Localize("Downloading update..."), showProgress: true);
            var progress = new Progress<ApplicationUpdateProgress>(p => ReportProgress(generation, p));
            PreparedApplicationUpdate prepared = await updateService.PrepareAsync(release, progress, token);
            updateCard.Subtitle = Localize("Update verified. Preparing to restart...");
            progressBar.Value = 0;
            if (!Owner.RequestApplicationUpdate(prepared))
            {
                prepared.Discard();
                SetIdle(Localize("Update could not be started."));
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            if (!Owner.IsDisposed)
            {
                SetIdle(Localize("Update cancelled."));
            }
        }
        catch (Exception ex)
        {
            AppLog.ForSource().LogError(ex, "Application update failed.");
            SetIdle(string.Format(Localize("Update failed: {0}"), ex.Message));
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
            updateCard.Subtitle = Localize("Verifying update...");
            return;
        }

        if (progress.TotalBytes is > 0)
        {
            int percent = (int)Math.Clamp(progress.BytesReceived * 100 / progress.TotalBytes.Value, 0, 100);
            progressBar.Value = percent;
            updateCard.Subtitle = string.Format(Localize("Downloading update... {0}%"), percent);
        }
        else
        {
            progressBar.Value = 0;
        }
    }

    private void SetBusy(string status, bool showProgress)
    {
        updateButton.IsEnabled = false;
        updateCard.Subtitle = status;
        progressBar.Visibility = showProgress ? Visibility.Visible : Visibility.Collapsed;
        progressBar.Value = 0;
    }

    private void SetIdle(string status)
    {
        updateButton.IsEnabled = true;
        updateCard.Subtitle = status;
        progressBar.Visibility = Visibility.Collapsed;
        progressBar.Value = 0;
    }

    private void CancelOperation()
    {
        operationCancellation?.Cancel();
        operationCancellation?.Dispose();
        operationCancellation = null;
    }
}
