using Avalonia.Interactivity;
using Microsoft.Extensions.Logging;
using WheelWizard.Localization;
using WheelWizard.MiiRendering.Services;
using WheelWizard.Services;
using WheelWizard.Shared.DependencyInjection;
using WheelWizard.Views.Popups.Base;

namespace WheelWizard.Views.Popups.Generic;

public partial class MiiRenderingSetupPopup : PopupContent
{
    private readonly TaskCompletionSource<bool> _completionSource = new();
    private CancellationTokenSource? _downloadCancellationTokenSource;
    private bool _downloadCompleted;
    private bool _skipRequested;

    [Inject]
    private IMiiRenderingResourceInstaller ResourceInstaller { get; set; } = null!;

    [Inject]
    private ILogger<MiiRenderingSetupPopup> Logger { get; set; } = null!;

    public MiiRenderingSetupPopup()
        : base(true, false, true, t("app.display_name"))
    {
        InitializeComponent();
        PathTextBlock.Text = PathManager.MiiRenderingResourceFilePath;
        StatusTextBlock.Text = t("popup.mii_rendering_download_prompt");
        ProgressTextBlock.Text = t("popup.ready_to_install");
    }

    public Task<bool> ShowAndAwaitCompletionAsync()
    {
        Show();
        return _completionSource.Task;
    }

    private async void DownloadButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_downloadCancellationTokenSource != null)
            return;

        ErrorTextBlock.IsVisible = false;
        SetBusyState(true, t("popup.downloading_required_file"));
        DownloadProgressBar.Value = 0;
        _downloadCancellationTokenSource = new CancellationTokenSource();

        var progress = new Progress<MiiRenderingInstallerProgress>(UpdateProgress);

        try
        {
            var result = await ResourceInstaller.DownloadAndInstallAsync(progress, _downloadCancellationTokenSource.Token);
            if (result.IsFailure)
            {
                Logger.LogWarning("Mii rendering setup failed: {Message}", result.Error?.Message);
                ShowError(result.Error?.Message ?? t("popup.mii_rendering_install_failed"));
                return;
            }

            _downloadCompleted = true;
            _completionSource.TrySetResult(true);
            Close();
        }
        finally
        {
            _downloadCancellationTokenSource?.Dispose();
            _downloadCancellationTokenSource = null;

            if (!_downloadCompleted)
                SetBusyState(false, t("popup.mii_rendering_download_prompt"));
        }
    }

    private void UpdateProgress(MiiRenderingInstallerProgress progress)
    {
        StatusTextBlock.Text = progress.Stage;

        if (progress.TotalBytes is > 0)
        {
            var percentage = Math.Clamp((progress.BytesDownloaded / (double)progress.TotalBytes.Value) * 100.0, 0, 100);
            DownloadProgressBar.Value = percentage;
            ProgressTextBlock.Text =
                $"{percentage:F0}% ({FormatMegabytes(progress.BytesDownloaded)} / {FormatMegabytes(progress.TotalBytes.Value)})";
            return;
        }

        DownloadProgressBar.Value = 0;
        ProgressTextBlock.Text =
            progress.BytesDownloaded > 0 ? t("popup.downloaded", FormatMegabytes(progress.BytesDownloaded)) : t("popup.working");
    }

    private void SkipButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_downloadCancellationTokenSource != null)
            return;

        _skipRequested = true;
        _completionSource.TrySetResult(true);
        Close();
    }

    protected override void BeforeClose()
    {
        _downloadCancellationTokenSource?.Cancel();
        _completionSource.TrySetResult(_downloadCompleted || _skipRequested);
    }

    private void SetBusyState(bool busy, string statusText)
    {
        StatusTextBlock.Text = statusText;
        DownloadButton.IsEnabled = !busy;
        SkipButton.IsEnabled = !busy;
    }

    private void ShowError(string message)
    {
        ErrorTextBlock.Text = message;
        ErrorTextBlock.IsVisible = true;
        SetBusyState(false, t("popup.mii_rendering_download_prompt"));
    }

    private static string FormatMegabytes(long bytes) => $"{bytes / 1024d / 1024d:F2} MB";
}
