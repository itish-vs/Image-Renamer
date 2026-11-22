using System.IO;
using System.Windows;
using ImageRenamer.Models;
using ImageRenamer.Services;

namespace ImageRenamer;

public partial class App : Application
{
    public AppSettings Settings { get; private set; } = new();
    public SettingsService SettingsService { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        SettingsService = new SettingsService(settingsPath);
        Settings = SettingsService.Load();

        var window = new MainWindow(Settings, SettingsService);
        MainWindow = window;
        window.Show();
    }
}
