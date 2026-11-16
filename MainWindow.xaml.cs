using System.ComponentModel;
using System.Windows;
using RabbitMqMonitor.Services;
using RabbitMqMonitor.ViewModels;

namespace RabbitMqMonitor;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly TrayNotificationService _trayService;
    private bool _isExplicitExit;

    public MainWindow(MainViewModel viewModel, TrayNotificationService trayService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _trayService = trayService;
        DataContext = _viewModel;
    }

    public void RequestExplicitExit()
    {
        _isExplicitExit = true;
        Close();
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState == WindowState.Minimized && _viewModel.MinimizeToTray)
        {
            Hide();
            _trayService.ShowMinimizedHint();
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isExplicitExit && _viewModel.MinimizeToTray)
        {
            e.Cancel = true;
            Hide();
            _trayService.ShowMinimizedHint();
            return;
        }

        _viewModel.Dispose();
        base.OnClosing(e);
    }
}

