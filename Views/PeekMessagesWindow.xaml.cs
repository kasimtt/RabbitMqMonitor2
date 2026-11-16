using System.Text;
using System.Windows;
using RabbitMqMonitor.Models;

namespace RabbitMqMonitor.Views;

public partial class PeekMessagesWindow : Window
{
    private readonly List<PeekedMessage> _messages;

    public PeekMessagesWindow(
        string serverName,
        string queueName,
        string vhost,
        List<PeekedMessage> messages)
    {
        InitializeComponent();
        _messages = messages;

        HeaderTitleText.Text = $"Queue: {queueName}";
        SubHeaderText.Text = $"Server: {serverName}   |   Virtual Host: {vhost}   |   Retrieved: {messages.Count} message(s)";

        MessagesListBox.ItemsSource = messages;
        EmptyStateText.Visibility = messages.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void CopyAllButton_Click(object sender, RoutedEventArgs e)
    {
        if (_messages.Count == 0)
            return;

        var sb = new StringBuilder();
        foreach (var msg in _messages)
        {
            sb.AppendLine($"--- Message #{msg.Index} (Exchange: {msg.Exchange}, RoutingKey: {msg.RoutingKey}, Bytes: {msg.PayloadBytes}) ---");
            sb.AppendLine(msg.FormattedPayload);
            sb.AppendLine();
        }

        Clipboard.SetText(sb.ToString());
        MessageBox.Show("All peeked message payloads copied to clipboard.", "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

