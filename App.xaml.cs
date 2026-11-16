using System.Windows;
using RabbitMqMonitor.Services;
using RabbitMqMonitor.ViewModels;

namespace RabbitMqMonitor;

public partial class App : Application
{
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var encryptionService = new CredentialEncryptionService();
        var storageService = new ServerConfigStorageService(encryptionService);
        var apiService = new RabbitMqApiService(encryptionService);

        TrayNotificationService? trayService = null;
        trayService = new TrayNotificationService(
            restoreWindowAction: RestoreMainWindow,
            exitApplicationAction: () => _mainWindow?.RequestExplicitExit());

        var mainViewModel = new MainViewModel(
            encryptionService,
            storageService,
            apiService,
            trayService);

        _mainWindow = new MainWindow(mainViewModel, trayService);
        MainWindow = _mainWindow;
        _mainWindow.Show();
    }

    private void RestoreMainWindow()
    {
        if (_mainWindow is null)
            return;

        _mainWindow.Show();
        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Normal;
        }
        _mainWindow.Activate();
    }
}

