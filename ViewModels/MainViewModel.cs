using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using RabbitMqMonitor.Models;
using RabbitMqMonitor.Services;
using RabbitMqMonitor.Views;

namespace RabbitMqMonitor.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly CredentialEncryptionService _encryptionService;
    private readonly ServerConfigStorageService _storageService;
    private readonly RabbitMqApiService _apiService;
    private readonly TrayNotificationService _trayService;
    private readonly DispatcherTimer _schedulerTimer;
    private readonly Dictionary<Guid, List<QueueItem>> _queuesByServer = new();
    private readonly SemaphoreSlim _pollLock = new(1, 1);

    private ServerProfile? _selectedServerFilter;
    private string _searchText = string.Empty;
    private bool _showOnlyNonEmptyQueues;
    private bool _showOnlyQueuesWithoutConsumers;
    private bool _isAutoRefreshEnabled = true;
    private int _selectedGlobalIntervalSeconds = 5;
    private bool _minimizeToTray = true;
    private bool _isPolling;
    private string _globalStatusBanner = "Ready";

    public ObservableCollection<ServerProfile> Servers { get; } = [];
    public ObservableCollection<QueueItem> AllQueues { get; } = [];
    public ICollectionView FilteredQueuesView { get; }

    public IReadOnlyList<int> AvailableRefreshIntervals { get; } = [3, 5, 10, 30];

    public ServerProfile? SelectedServerFilter
    {
        get => _selectedServerFilter;
        set
        {
            if (SetProperty(ref _selectedServerFilter, value))
            {
                OnPropertyChanged(nameof(IsAllServersSelected));
                OnPropertyChanged(nameof(ActiveFilterTitle));
                FilteredQueuesView.Refresh();
            }
        }
    }

    public bool IsAllServersSelected => SelectedServerFilter is null;

    public string ActiveFilterTitle => SelectedServerFilter is null
        ? "All Servers (Unified Queue Stream)"
        : $"Filtered by Server: {SelectedServerFilter.Name} ({SelectedServerFilter.HostUrl})";

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                FilteredQueuesView.Refresh();
            }
        }
    }

    public bool ShowOnlyNonEmptyQueues
    {
        get => _showOnlyNonEmptyQueues;
        set
        {
            if (SetProperty(ref _showOnlyNonEmptyQueues, value))
            {
                FilteredQueuesView.Refresh();
            }
        }
    }

    public bool ShowOnlyQueuesWithoutConsumers
    {
        get => _showOnlyQueuesWithoutConsumers;
        set
        {
            if (SetProperty(ref _showOnlyQueuesWithoutConsumers, value))
            {
                FilteredQueuesView.Refresh();
            }
        }
    }

    public bool IsAutoRefreshEnabled
    {
        get => _isAutoRefreshEnabled;
        set
        {
            if (SetProperty(ref _isAutoRefreshEnabled, value))
            {
                OnPropertyChanged(nameof(AutoRefreshButtonText));
                GlobalStatusBanner = value
                    ? $"Auto-refresh active (Global: {SelectedGlobalIntervalSeconds}s)"
                    : "Auto-refresh paused";
            }
        }
    }

    public string AutoRefreshButtonText => IsAutoRefreshEnabled ? "⏸ Pause Polling" : "▶ Resume Polling";

    public int SelectedGlobalIntervalSeconds
    {
        get => _selectedGlobalIntervalSeconds;
        set
        {
            if (SetProperty(ref _selectedGlobalIntervalSeconds, value))
            {
                foreach (var s in Servers)
                {
                    s.RefreshIntervalSeconds = value;
                }
                _storageService.SaveServers(Servers);
                GlobalStatusBanner = $"Global refresh interval set to {value}s";
            }
        }
    }

    public bool MinimizeToTray
    {
        get => _minimizeToTray;
        set => SetProperty(ref _minimizeToTray, value);
    }

    public bool IsPolling
    {
        get => _isPolling;
        set => SetProperty(ref _isPolling, value);
    }

    public string GlobalStatusBanner
    {
        get => _globalStatusBanner;
        set => SetProperty(ref _globalStatusBanner, value);
    }

    // --- Summary KPIs across all servers ---
    public int OnlineServersCount => Servers.Count(s => s.IsEnabled && s.IsOnline);
    public int EnabledServersCount => Servers.Count(s => s.IsEnabled);
    public int TotalQueuesCount => AllQueues.Count;
    public long TotalMessagesCount => AllQueues.Sum(q => q.TotalMessages);
    public int AlertQueuesCount => AllQueues.Count(q => q.IsOverThreshold || q.IsStalledWithoutConsumers);

    // --- Commands ---
    public AsyncRelayCommand RefreshNowCommand { get; }
    public RelayCommand ToggleAutoRefreshCommand { get; }
    public RelayCommand SelectAllServersCommand { get; }
    public RelayCommand SelectServerFilterCommand { get; }
    public RelayCommand AddServerCommand { get; }
    public RelayCommand EditServerCommand { get; }
    public RelayCommand DeleteServerCommand { get; }
    public AsyncRelayCommand ToggleServerEnabledCommand { get; }
    public AsyncRelayCommand PeekQueueMessagesCommand { get; }

    public MainViewModel(
        CredentialEncryptionService encryptionService,
        ServerConfigStorageService storageService,
        RabbitMqApiService apiService,
        TrayNotificationService trayService)
    {
        _encryptionService = encryptionService;
        _storageService = storageService;
        _apiService = apiService;
        _trayService = trayService;

        FilteredQueuesView = CollectionViewSource.GetDefaultView(AllQueues);
        FilteredQueuesView.Filter = FilterQueuePredicate;
        FilteredQueuesView.SortDescriptions.Add(new SortDescription(nameof(QueueItem.TotalMessages), ListSortDirection.Descending));
        FilteredQueuesView.SortDescriptions.Add(new SortDescription(nameof(QueueItem.ServerName), ListSortDirection.Ascending));
        FilteredQueuesView.SortDescriptions.Add(new SortDescription(nameof(QueueItem.Name), ListSortDirection.Ascending));

        RefreshNowCommand = new AsyncRelayCommand(() => PollServersAsync(forceAll: true));
        ToggleAutoRefreshCommand = new RelayCommand(() => IsAutoRefreshEnabled = !IsAutoRefreshEnabled);
        SelectAllServersCommand = new RelayCommand(() => SelectedServerFilter = null);
        SelectServerFilterCommand = new RelayCommand(param =>
        {
            if (param is ServerProfile server)
            {
                SelectedServerFilter = SelectedServerFilter == server ? null : server;
            }
        });

        AddServerCommand = new RelayCommand(AddServer);
        EditServerCommand = new RelayCommand(EditServer);
        DeleteServerCommand = new RelayCommand(DeleteServer);
        ToggleServerEnabledCommand = new AsyncRelayCommand(ToggleServerEnabledAsync);
        PeekQueueMessagesCommand = new AsyncRelayCommand(PeekQueueMessagesAsync);

        foreach (var profile in _storageService.LoadServers())
        {
            Servers.Add(profile);
        }

        _schedulerTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _schedulerTimer.Tick += async (_, _) => await OnSchedulerTickAsync();
        _schedulerTimer.Start();

        _ = PollServersAsync(forceAll: true);
    }

    private bool FilterQueuePredicate(object obj)
    {
        if (obj is not QueueItem queue)
            return false;

        if (SelectedServerFilter is not null && queue.ServerId != SelectedServerFilter.Id)
            return false;

        if (ShowOnlyNonEmptyQueues && queue.TotalMessages <= 0)
            return false;

        if (ShowOnlyQueuesWithoutConsumers && queue.Consumers > 0)
            return false;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            string term = SearchText.Trim();
            bool matchesQueue = queue.Name.Contains(term, StringComparison.OrdinalIgnoreCase);
            bool matchesServer = queue.ServerName.Contains(term, StringComparison.OrdinalIgnoreCase);
            bool matchesVhost = queue.VirtualHost.Contains(term, StringComparison.OrdinalIgnoreCase);
            if (!matchesQueue && !matchesServer && !matchesVhost)
                return false;
        }

        return true;
    }

    private async Task OnSchedulerTickAsync()
    {
        if (!IsAutoRefreshEnabled || IsPolling)
            return;

        await PollServersAsync(forceAll: false);
    }

    public async Task PollServersAsync(bool forceAll)
    {
        if (!await _pollLock.WaitAsync(0))
            return;

        try
        {
            DateTime nowUtc = DateTime.UtcNow;
            var dueServers = Servers
                .Where(s => s.IsEnabled &&
                            (forceAll || (nowUtc - s.LastPollUtc).TotalSeconds >= Math.Max(1, s.RefreshIntervalSeconds)))
                .ToList();

            foreach (var disabled in Servers.Where(s => !s.IsEnabled))
            {
                disabled.IsOnline = false;
                disabled.StatusText = "DISABLED";
                disabled.ErrorMessage = "Server polling is disabled";
                _queuesByServer.Remove(disabled.Id);
            }

            if (dueServers.Count == 0)
            {
                RebuildQueueCollection();
                return;
            }

            IsPolling = true;
            GlobalStatusBanner = $"Polling {dueServers.Count} server(s)...";

            // Poll all due servers concurrently so slow/offline servers never delay healthy ones
            var pollTasks = dueServers.Select(async server =>
            {
                server.LastPollUtc = DateTime.UtcNow;
                var pollResult = await _apiService.PollServerAsync(server);
                return (Server: server, Result: pollResult);
            });

            var results = await Task.WhenAll(pollTasks);

            foreach (var (server, res) in results)
            {
                bool wasOnline = server.HasChecked && server.IsOnline;
                server.HasChecked = true;
                server.IsOnline = res.IsOnline;
                server.StatusText = res.StatusText;
                server.ErrorMessage = res.ErrorMessage;

                if (res.IsOnline)
                {
                    server.RabbitMqVersion = res.RabbitMqVersion;
                    server.ErlangVersion = res.ErlangVersion;
                    server.GlobalPublishRate = res.GlobalPublishRate;
                    server.GlobalDeliverRate = res.GlobalDeliverRate;
                    server.TotalQueues = res.Queues.Count;
                    server.TotalMessages = res.Queues.Sum(q => q.TotalMessages);
                    server.ActiveConsumers = res.Queues.Sum(q => q.Consumers);
                    server.LastChecked = res.CheckedAt;

                    _queuesByServer[server.Id] = res.Queues;

                    foreach (var q in res.Queues.Where(q => q.IsOverThreshold))
                    {
                        _trayService.NotifyQueueThresholdExceeded(
                            server.Name,
                            q.Name,
                            q.TotalMessages,
                            server.QueueAlertThreshold);
                    }
                }
                else
                {
                    server.TotalQueues = 0;
                    server.TotalMessages = 0;
                    server.ActiveConsumers = 0;
                    server.GlobalPublishRate = 0;
                    server.GlobalDeliverRate = 0;
                    _queuesByServer.Remove(server.Id);

                    if (wasOnline || forceAll)
                    {
                        _trayService.NotifyServerOffline(server.Name, res.StatusText);
                    }
                }
            }

            RebuildQueueCollection();
            GlobalStatusBanner = $"Last updated at {DateTime.Now:HH:mm:ss} — {OnlineServersCount}/{EnabledServersCount} active servers online";
        }
        finally
        {
            IsPolling = false;
            _pollLock.Release();
        }
    }

    private void RebuildQueueCollection()
    {
        AllQueues.Clear();
        foreach (var server in Servers.Where(s => s.IsEnabled))
        {
            if (_queuesByServer.TryGetValue(server.Id, out var queues))
            {
                foreach (var q in queues)
                {
                    q.ServerName = server.Name;
                    q.AlertThreshold = server.QueueAlertThreshold;
                    AllQueues.Add(q);
                }
            }
        }

        FilteredQueuesView.Refresh();
        OnPropertyChanged(nameof(OnlineServersCount));
        OnPropertyChanged(nameof(EnabledServersCount));
        OnPropertyChanged(nameof(TotalQueuesCount));
        OnPropertyChanged(nameof(TotalMessagesCount));
        OnPropertyChanged(nameof(AlertQueuesCount));
    }

    private void AddServer()
    {
        var draft = new ServerProfile
        {
            Name = $"RabbitMQ-{Servers.Count + 1}",
            HostUrl = "http://localhost:15672",
            Username = "guest",
            EncryptedPassword = _encryptionService.Encrypt("guest"),
            VirtualHost = "/",
            RefreshIntervalSeconds = SelectedGlobalIntervalSeconds,
            QueueAlertThreshold = 1000,
            IsEnabled = true
        };

        var dialog = new ServerEditorWindow(draft, _encryptionService, isEditMode: false)
        {
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() == true)
        {
            Servers.Add(draft);
            _storageService.SaveServers(Servers);
            _ = PollServersAsync(forceAll: true);
        }
    }

    private void EditServer(object? param)
    {
        if (param is not ServerProfile target)
            return;

        var editableCopy = target.Clone();
        var dialog = new ServerEditorWindow(editableCopy, _encryptionService, isEditMode: true)
        {
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() == true)
        {
            target.Name = editableCopy.Name;
            target.HostUrl = editableCopy.HostUrl;
            target.Username = editableCopy.Username;
            target.EncryptedPassword = editableCopy.EncryptedPassword;
            target.VirtualHost = editableCopy.VirtualHost;
            target.RefreshIntervalSeconds = editableCopy.RefreshIntervalSeconds;
            target.QueueAlertThreshold = editableCopy.QueueAlertThreshold;
            target.IsEnabled = editableCopy.IsEnabled;

            _storageService.SaveServers(Servers);
            _ = PollServersAsync(forceAll: true);
        }
    }

    private void DeleteServer(object? param)
    {
        if (param is not ServerProfile target)
            return;

        var answer = MessageBox.Show(
            $"Remove server profile '{target.Name}' ({target.HostUrl})?",
            "Confirm Delete Server",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (answer != MessageBoxResult.Yes)
            return;

        if (SelectedServerFilter == target)
            SelectedServerFilter = null;

        _queuesByServer.Remove(target.Id);
        Servers.Remove(target);
        _storageService.SaveServers(Servers);
        RebuildQueueCollection();
    }

    private async Task ToggleServerEnabledAsync(object? param)
    {
        if (param is not ServerProfile)
            return;

        _storageService.SaveServers(Servers);
        await PollServersAsync(forceAll: true);
    }

    private async Task PeekQueueMessagesAsync(object? param)
    {
        if (param is not QueueItem queueItem)
            return;

        var server = Servers.FirstOrDefault(s => s.Id == queueItem.ServerId);
        if (server is null)
            return;

        GlobalStatusBanner = $"Peeking top 5 messages from '{queueItem.Name}' on {server.Name} (ack_requeue_true)...";
        var (success, error, messages) = await _apiService.PeekMessagesAsync(
            server,
            queueItem.VirtualHost,
            queueItem.Name,
            count: 5);

        if (!success)
        {
            MessageBox.Show(
                $"Failed to peek messages from '{queueItem.Name}':\n{error}",
                "Peek Messages Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            GlobalStatusBanner = $"Peek failed on '{queueItem.Name}': {error}";
            return;
        }

        GlobalStatusBanner = $"Peeked {messages.Count} message(s) from '{queueItem.Name}' (non-destructive requeue)";
        var modal = new PeekMessagesWindow(server.Name, queueItem.Name, queueItem.VirtualHost, messages)
        {
            Owner = Application.Current.MainWindow
        };
        modal.ShowDialog();
    }

    public void Dispose()
    {
        _schedulerTimer.Stop();
        _apiService.Dispose();
        _trayService.Dispose();
        _pollLock.Dispose();
    }
}

