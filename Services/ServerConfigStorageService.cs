using System.IO;
using System.Text.Json;
using RabbitMqMonitor.Models;

namespace RabbitMqMonitor.Services;

/// <summary>
/// Persists server profiles in servers.json next to the executable.
/// Passwords are stored exclusively as DPAPI-encrypted Base64 strings.
/// </summary>
public sealed class ServerConfigStorageService
{
    private readonly CredentialEncryptionService _encryptionService;
    private readonly string _configFilePath;
    private readonly string _settingsFilePath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ServerConfigStorageService(CredentialEncryptionService encryptionService)
    {
        _encryptionService = encryptionService;
        string baseDir = AppContext.BaseDirectory;
        _configFilePath = Path.Combine(baseDir, "servers.json");
        _settingsFilePath = Path.Combine(baseDir, "settings.json");
    }

    public string ConfigFilePath => _configFilePath;

    public bool LoadRunInBackgroundSetting()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                string json = File.ReadAllText(_settingsFilePath);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("runInBackground", out var prop) &&
                    (prop.ValueKind == JsonValueKind.True || prop.ValueKind == JsonValueKind.False))
                {
                    return prop.GetBoolean();
                }
            }
        }
        catch
        {
            // Ignore and use default
        }
        return true;
    }

    public void SaveRunInBackgroundSetting(bool runInBackground)
    {
        try
        {
            string json = JsonSerializer.Serialize(new { runInBackground }, JsonOptions);
            File.WriteAllText(_settingsFilePath, json);
        }
        catch
        {
            // Ignore write errors
        }
    }

    public List<ServerProfile> LoadServers()
    {
        try
        {
            if (!File.Exists(_configFilePath))
            {
                var defaults = CreateDefaultProfiles();
                SaveServers(defaults);
                return defaults;
            }

            string json = File.ReadAllText(_configFilePath);
            var servers = JsonSerializer.Deserialize<List<ServerProfile>>(json, JsonOptions);
            if (servers is { Count: > 0 })
                return servers;
        }
        catch (Exception)
        {
            // Fall back to default profiles if JSON is unreadable
        }

        return CreateDefaultProfiles();
    }

    public void SaveServers(IEnumerable<ServerProfile> servers)
    {
        string json = JsonSerializer.Serialize(servers.ToList(), JsonOptions);
        string tempPath = _configFilePath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _configFilePath, overwrite: true);
    }

    private List<ServerProfile> CreateDefaultProfiles()
    {
        return
        [
            new ServerProfile
            {
                Id = Guid.NewGuid(),
                Name = "Local Dev",
                HostUrl = "http://localhost:15672",
                Username = "guest",
                EncryptedPassword = _encryptionService.Encrypt("guest"),
                VirtualHost = "/",
                RefreshIntervalSeconds = 5,
                QueueAlertThreshold = 1000,
                IsEnabled = true
            }
        ];
    }
}

