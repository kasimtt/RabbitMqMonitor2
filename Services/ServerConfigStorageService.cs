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
    }

    public string ConfigFilePath => _configFilePath;

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

