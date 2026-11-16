using System.Drawing;
using System.Windows;
using Forms = System.Windows.Forms;

namespace RabbitMqMonitor.Services;

/// <summary>
/// Manages the Windows System Tray icon and native desktop balloon notifications
/// with cooldown tracking to avoid alert fatigue.
/// </summary>
public sealed class TrayNotificationService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Icon _trayIcon;
    private readonly Action _restoreWindowAction;
    private readonly Action _exitApplicationAction;
    private readonly Dictionary<string, DateTime> _alertCooldowns = new();
    private readonly TimeSpan _cooldownWindow = TimeSpan.FromMinutes(3);

    public bool NotificationsEnabled { get; set; } = true;

    public TrayNotificationService(Action restoreWindowAction, Action exitApplicationAction)
    {
        _restoreWindowAction = restoreWindowAction;
        _exitApplicationAction = exitApplicationAction;
        _trayIcon = CreateDynamicRabbitIcon();

        var contextMenu = new Forms.ContextMenuStrip();
        var openItem = new Forms.ToolStripMenuItem("Open RabbitMQ Dashboard");
        openItem.Click += (_, _) => Application.Current.Dispatcher.Invoke(_restoreWindowAction);

        var muteItem = new Forms.ToolStripMenuItem("Enable Desktop Alerts")
        {
            Checked = true,
            CheckOnClick = true
        };
        muteItem.CheckedChanged += (_, _) => NotificationsEnabled = muteItem.Checked;

        var exitItem = new Forms.ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => Application.Current.Dispatcher.Invoke(_exitApplicationAction);

        contextMenu.Items.Add(openItem);
        contextMenu.Items.Add(muteItem);
        contextMenu.Items.Add(new Forms.ToolStripSeparator());
        contextMenu.Items.Add(exitItem);

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = _trayIcon,
            Text = "RabbitMQ Multi-Server Monitor",
            Visible = true,
            ContextMenuStrip = contextMenu
        };

        _notifyIcon.DoubleClick += (_, _) => Application.Current.Dispatcher.Invoke(_restoreWindowAction);
        _notifyIcon.BalloonTipClicked += (_, _) => Application.Current.Dispatcher.Invoke(_restoreWindowAction);
    }

    public void NotifyServerOffline(string serverName, string reason)
    {
        string key = $"offline:{serverName}";
        ShowAlertIfCooledDown(
            key,
            $"Server Offline: {serverName}",
            $"Status: {reason}",
            Forms.ToolTipIcon.Error);
    }

    public void NotifyQueueThresholdExceeded(string serverName, string queueName, long totalMessages, int threshold)
    {
        string key = $"threshold:{serverName}:{queueName}";
        ShowAlertIfCooledDown(
            key,
            $"Queue Alert on {serverName}",
            $"Queue '{queueName}' has {totalMessages:N0} messages (Threshold: {threshold:N0}).",
            Forms.ToolTipIcon.Warning);
    }

    public void ShowMinimizedHint()
    {
        ShowAlertIfCooledDown(
            "tray:minimized",
            "RabbitMQ Monitor Running in Background",
            "Double-click the system tray icon to restore the dashboard.",
            Forms.ToolTipIcon.Info);
    }

    private void ShowAlertIfCooledDown(string key, string title, string message, Forms.ToolTipIcon icon)
    {
        if (!NotificationsEnabled)
            return;

        DateTime now = DateTime.UtcNow;
        if (_alertCooldowns.TryGetValue(key, out DateTime lastAlert) && (now - lastAlert) < _cooldownWindow)
            return;

        _alertCooldowns[key] = now;
        _notifyIcon.ShowBalloonTip(4000, title, message, icon);
    }

    private static Icon CreateDynamicRabbitIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        using var bgBrush = new SolidBrush(Color.FromArgb(255, 255, 102, 0)); // RabbitMQ Orange
        g.FillEllipse(bgBrush, 2, 2, 28, 28);

        using var earBrush = new SolidBrush(Color.White);
        g.FillRectangle(earBrush, 10, 7, 4, 11);
        g.FillRectangle(earBrush, 18, 7, 4, 11);
        g.FillRectangle(earBrush, 9, 15, 14, 9);

        IntPtr hIcon = bmp.GetHicon();
        return Icon.FromHandle(hIcon);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _trayIcon.Dispose();
    }
}

