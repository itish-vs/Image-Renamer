using System.IO;
using System.Windows;
using Microsoft.Win32;
using ImageRenamer.Models;
using ImageRenamer.Services;

namespace ImageRenamer;

public partial class MainWindow : Window
{
    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;
    private readonly ImageRenameService _renameService = new();
    private bool _isBusy;
    private int _imageCount;

    public MainWindow(AppSettings settings, SettingsService settingsService)
    {
        InitializeComponent();
        _settings = settings;
        _settingsService = settingsService;

        FolderPathTextBox.Text = _settings.ImageRenamer.ImageFolder;
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var settingsLoadError = _settingsService.LoadError;
        await RefreshCountAsync();

        if (!string.IsNullOrWhiteSpace(settingsLoadError))
        {
            SetStatus("Could not read appsettings.json. Default settings are loaded.", settingsLoadError);
        }
    }

    private async void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Choose folder containing images",
            InitialDirectory = Directory.Exists(_settings.ImageRenamer.ImageFolder)
                ? _settings.ImageRenamer.ImageFolder
                : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        _settings.ImageRenamer.ImageFolder = dialog.FolderName;
        FolderPathTextBox.Text = dialog.FolderName;

        try
        {
            _settingsService.Save(_settings);
            SetStatus("Folder saved to appsettings.json.", "Folder selection updated.");
        }
        catch (Exception ex)
        {
            SetStatus("Folder selected for this session. Could not save appsettings.json.", ex.Message);
        }

        await RefreshCountAsync();
    }

    private async void RescanButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshCountAsync();
    }

    private async void RenameButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        var folder = _settings.ImageRenamer.ImageFolder;
        if (!Directory.Exists(folder))
        {
            SetStatus("Selected folder could not be found.", folder);
            return;
        }

        var currentCount = _imageCount;
        if (currentCount == 0)
        {
            SetStatus("No supported images to rename.", "Choose another folder or update SupportedExtensions in appsettings.json.");
            return;
        }


        SetBusy(true);
        RenameProgressBar.Value = 0;
        ProgressPercentText.Text = "0%";
        SetStatus("Starting rename…", "Do not move or edit files in the selected folder while the operation is running.");

        var progress = new Progress<RenameProgress>(update =>
        {
            RenameProgressBar.Value = update.Percentage;
            ProgressPercentText.Text = $"{update.Percentage}%";
            StatusText.Text = update.Message;
            ProgressDetailText.Text = update.Total > 0
                ? $"{update.Completed:N0} of {update.Total:N0} images"
                : "Preparing files";
        });

        var renameCompleted = false;

        try
        {
            var result = await _renameService.RenameAsync(
                folder,
                _settings.ImageRenamer.SupportedExtensions,
                _settings.ImageRenamer.TempFolderPrefix,
                progress);

            if (result.Success)
            {
                RenameProgressBar.Value = 100;
                ProgressPercentText.Text = "100%";
                SetStatus(result.Message, "All file extensions were preserved.");
                renameCompleted = true;

                MessageBox.Show(
                    $"Process complete.\n\n{result.Message}",
                    "Process complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else if (!string.IsNullOrWhiteSpace(result.RecoveryFolder))
            {
                SetStatus(result.Message, $"Recovery folder: {result.RecoveryFolder}");
                MessageBox.Show(
                    $"{result.Message}\n\nRecovery folder:\n{result.RecoveryFolder}",
                    "Recovery required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            else
            {
                SetStatus(result.Message, "No files were intentionally deleted.");
            }
        }
        catch (Exception ex)
        {
            SetStatus("Unexpected error while renaming images.", ex.Message);
            MessageBox.Show(ex.Message, "Image Renamer", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false);
            // After success, return the window to its ready state and refresh
            // the folder count. Keep error/recovery details visible on failure.
            await RefreshCountAsync(keepStatus: !renameCompleted);
        }
    }

    private async Task RefreshCountAsync(bool keepStatus = false)
    {
        if (_isBusy)
        {
            return;
        }

        SetBusy(true);
        try
        {
            var files = await _renameService.GetImagesAsync(
                _settings.ImageRenamer.ImageFolder,
                _settings.ImageRenamer.SupportedExtensions);

            _imageCount = files.Count;
            ImageCountText.Text = files.Count.ToString("N0");
            RenameButton.IsEnabled = files.Count > 0;

            if (!keepStatus)
            {
                SetStatus(
                    files.Count == 0 ? "No supported images found." : "Ready to rename.",
                    $"Scanned folder: {_settings.ImageRenamer.ImageFolder}");
                RenameProgressBar.Value = 0;
                ProgressPercentText.Text = "0%";
            }
        }
        catch (Exception ex)
        {
            _imageCount = 0;
            ImageCountText.Text = "0";
            RenameButton.IsEnabled = false;
            if (!keepStatus)
            {
                SetStatus("Unable to scan selected folder.", ex.Message);
            }
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool isBusy)
    {
        _isBusy = isBusy;
        BrowseButton.IsEnabled = !isBusy;
        RescanButton.IsEnabled = !isBusy;
        RenameButton.IsEnabled = !isBusy && _imageCount > 0;
        Cursor = isBusy ? System.Windows.Input.Cursors.Wait : System.Windows.Input.Cursors.Arrow;
    }

    private void SetStatus(string status, string detail)
    {
        StatusText.Text = status;
        ProgressDetailText.Text = detail;
    }
}
