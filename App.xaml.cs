using System.Threading;
using System.Windows;
using RabbitMqMonitor.Services;
using RabbitMqMonitor.ViewModels;

namespace RabbitMqMonitor;

public partial class App : Application
{
    private const string SingleInstanceMutexName = @"Local\RabbitMqMonitor_SingleInstance_Mutex_v1";
    private const string ActivateExistingEventName = @"Local\RabbitMqMonitor_ActivateWindow_Event_v1";

    private Mutex? _instanceMutex;
    private EventWaitHandle? _activateEvent;
    private RegisteredWaitHandle? _registeredWait;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        _instanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out bool isFirstInstance);
        if (!isFirstInstance)
        {
            // Another instance is already running (in foreground or system tray).
            // Signal the existing instance to restore and focus its window, then exit immediately.
            try
            {
                using var existingEvent = EventWaitHandle.OpenExisting(ActivateExistingEventName);
                existingEvent.Set();
            }
            catch
            {
                // Ignore if event handle could not be opened
            }

            Shutdown();
            return;
        }

        _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateExistingEventName);
        _registeredWait = ThreadPool.RegisterWaitForSingleObject(
            _activateEvent,
            (_, _) => Dispatcher.BeginInvoke(RestoreMainWindow),
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);

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

        if (!_mainWindow.IsVisible)
        {
            _mainWindow.Show();
        }

        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Normal;
        }

        // Bring window to front reliably on Windows even when woken from background
        bool wasTopmost = _mainWindow.Topmost;
        _mainWindow.Topmost = true;
        _mainWindow.Activate();
        _mainWindow.Focus();
        _mainWindow.Topmost = wasTopmost;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _registeredWait?.Unregister(null);
        _activateEvent?.Dispose();

        if (_instanceMutex is not null)
        {
            try
            {
                _instanceMutex.ReleaseMutex();
            }
            catch
            {
                // Ignore if not owned
            }
            _instanceMutex.Dispose();
        }

        base.OnExit(e);
    }
}
