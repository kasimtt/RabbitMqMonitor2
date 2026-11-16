using System.Text.Json.Serialization;
using RabbitMqMonitor.ViewModels;

namespace RabbitMqMonitor.Models;

public class ServerProfile : ObservableObject
{
    private Guid _id = Guid.NewGuid();
    private string _name = "Local RabbitMQ";
    private string _hostUrl = "http://localhost:15672";
    private string _username = "guest";
    private string _encryptedPassword = string.Empty;
    private string _virtualHost = "/";
    private int _refreshIntervalSeconds = 5;
    private int _queueAlertThreshold = 1000;
    private bool _isEnabled = true;

    public Guid Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string HostUrl
    {
        get => _hostUrl;
        set => SetProperty(ref _hostUrl, value?.TrimEnd('/') ?? string.Empty);
    }

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    /// <summary>
    /// Base64-encoded DPAPI-encrypted password stored on disk in servers.json.
    /// </summary>
    public string EncryptedPassword
    {
        get => _encryptedPassword;
        set => SetProperty(ref _encryptedPassword, value);
    }

    public string VirtualHost
    {
        get => _virtualHost;
        set => SetProperty(ref _virtualHost, string.IsNullOrWhiteSpace(value) ? "/" : value.Trim());
    }

    public int RefreshIntervalSeconds
    {
        get => _refreshIntervalSeconds;
        set => SetProperty(ref _refreshIntervalSeconds, Math.Max(1, value));
    }

    public int QueueAlertThreshold
    {
        get => _queueAlertThreshold;
        set => SetProperty(ref _queueAlertThreshold, Math.Max(1, value));
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    // --- Live Runtime Telemetry (Not persisted to JSON) ---

    private bool _isOnline;
    private bool _hasChecked;
    private string _statusText = "Pending...";
    private string _errorMessage = string.Empty;
    private string _rabbitMqVersion = "-";
    private string _erlangVersion = "-";
    private int _totalQueues;
    private long _totalMessages;
    private int _activeConsumers;
    private double _globalPublishRate;
    private double _globalDeliverRate;
    private DateTime? _lastChecked;
    private DateTime _lastPollUtc = DateTime.MinValue;

    [JsonIgnore]
    public bool IsOnline
    {
        get => _isOnline;
        set => SetProperty(ref _isOnline, value);
    }

    [JsonIgnore]
    public bool HasChecked
    {
        get => _hasChecked;
        set => SetProperty(ref _hasChecked, value);
    }

    [JsonIgnore]
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    [JsonIgnore]
    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    [JsonIgnore]
    public string RabbitMqVersion
    {
        get => _rabbitMqVersion;
        set => SetProperty(ref _rabbitMqVersion, value);
    }

    [JsonIgnore]
    public string ErlangVersion
    {
        get => _erlangVersion;
        set => SetProperty(ref _erlangVersion, value);
    }

    [JsonIgnore]
    public int TotalQueues
    {
        get => _totalQueues;
        set => SetProperty(ref _totalQueues, value);
    }

    [JsonIgnore]
    public long TotalMessages
    {
        get => _totalMessages;
        set => SetProperty(ref _totalMessages, value);
    }

    [JsonIgnore]
    public int ActiveConsumers
    {
        get => _activeConsumers;
        set => SetProperty(ref _activeConsumers, value);
    }

    [JsonIgnore]
    public double GlobalPublishRate
    {
        get => _globalPublishRate;
        set => SetProperty(ref _globalPublishRate, value);
    }

    [JsonIgnore]
    public double GlobalDeliverRate
    {
        get => _globalDeliverRate;
        set => SetProperty(ref _globalDeliverRate, value);
    }

    [JsonIgnore]
    public DateTime? LastChecked
    {
        get => _lastChecked;
        set
        {
            if (SetProperty(ref _lastChecked, value))
            {
                OnPropertyChanged(nameof(LastCheckedDisplay));
            }
        }
    }

    [JsonIgnore]
    public string LastCheckedDisplay => LastChecked.HasValue
        ? LastChecked.Value.ToString("HH:mm:ss")
        : "Never";

    [JsonIgnore]
    public DateTime LastPollUtc
    {
        get => _lastPollUtc;
        set => _lastPollUtc = value;
    }

    public ServerProfile Clone()
    {
        return new ServerProfile
        {
            Id = Id,
            Name = Name,
            HostUrl = HostUrl,
            Username = Username,
            EncryptedPassword = EncryptedPassword,
            VirtualHost = VirtualHost,
            RefreshIntervalSeconds = RefreshIntervalSeconds,
            QueueAlertThreshold = QueueAlertThreshold,
            IsEnabled = IsEnabled
        };
    }
}

