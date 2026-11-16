using System.Windows;
using RabbitMqMonitor.Models;
using RabbitMqMonitor.Services;

namespace RabbitMqMonitor.Views;

public partial class ServerEditorWindow : Window
{
    private readonly ServerProfile _profile;
    private readonly CredentialEncryptionService _encryptionService;

    public ServerEditorWindow(
        ServerProfile profile,
        CredentialEncryptionService encryptionService,
        bool isEditMode)
    {
        InitializeComponent();
        _profile = profile;
        _encryptionService = encryptionService;

        DialogTitleText.Text = isEditMode ? $"Edit Server — {profile.Name}" : "Add RabbitMQ Server Profile";
        NameTextBox.Text = profile.Name;
        HostUrlTextBox.Text = profile.HostUrl;
        UsernameTextBox.Text = profile.Username;
        PasswordInput.Password = _encryptionService.Decrypt(profile.EncryptedPassword);
        VhostTextBox.Text = profile.VirtualHost;
        RefreshIntervalTextBox.Text = profile.RefreshIntervalSeconds.ToString();
        AlertThresholdTextBox.Text = profile.QueueAlertThreshold.ToString();
        IsEnabledCheckBox.IsChecked = profile.IsEnabled;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        string name = NameTextBox.Text.Trim();
        string hostUrl = HostUrlTextBox.Text.Trim().TrimEnd('/');

        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Please enter a server name.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!Uri.TryCreate(hostUrl, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            MessageBox.Show("Please enter a valid HTTP or HTTPS Management URL (e.g., http://192.168.1.10:15672).",
                "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(RefreshIntervalTextBox.Text.Trim(), out int interval) || interval < 1)
        {
            MessageBox.Show("Refresh interval must be a positive integer (seconds).",
                "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(AlertThresholdTextBox.Text.Trim(), out int threshold) || threshold < 1)
        {
            MessageBox.Show("Queue alert threshold must be a positive integer.",
                "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _profile.Name = name;
        _profile.HostUrl = hostUrl;
        _profile.Username = string.IsNullOrWhiteSpace(UsernameTextBox.Text) ? "guest" : UsernameTextBox.Text.Trim();
        _profile.EncryptedPassword = _encryptionService.Encrypt(PasswordInput.Password);
        _profile.VirtualHost = string.IsNullOrWhiteSpace(VhostTextBox.Text) ? "/" : VhostTextBox.Text.Trim();
        _profile.RefreshIntervalSeconds = interval;
        _profile.QueueAlertThreshold = threshold;
        _profile.IsEnabled = IsEnabledCheckBox.IsChecked ?? true;

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

