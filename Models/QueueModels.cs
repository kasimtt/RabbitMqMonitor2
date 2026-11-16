using System.Text.Json.Serialization;

namespace RabbitMqMonitor.Models;

public class QueueItem
{
    public Guid ServerId { get; set; }
    public string ServerName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string VirtualHost { get; set; } = "/";
    public string State { get; set; } = "unknown";
    public long TotalMessages { get; set; }
    public long ReadyMessages { get; set; }
    public long UnackedMessages { get; set; }
    public int Consumers { get; set; }
    public double PublishRate { get; set; }
    public double DeliverRate { get; set; }
    public int AlertThreshold { get; set; } = 1000;

    public bool IsOverThreshold => TotalMessages >= AlertThreshold;
    public bool IsStalledWithoutConsumers => TotalMessages > 0 && Consumers == 0;

    /// <summary>
    /// Severity level for DataGrid row highlighting:
    /// "Critical" = Over server's QueueAlertThreshold,
    /// "Warning"  = TotalMessages > 0 and Consumers == 0,
    /// "Normal"   = Healthy
    /// </summary>
    public string AlertLevel
    {
        get
        {
            if (IsOverThreshold) return "Critical";
            if (IsStalledWithoutConsumers) return "Warning";
            return "Normal";
        }
    }

    public string UniqueKey => $"{ServerId}:{VirtualHost}:{Name}";
}

public class PeekedMessage
{
    public int Index { get; set; }
    public string Exchange { get; set; } = string.Empty;
    public string RoutingKey { get; set; } = string.Empty;
    public bool Redelivered { get; set; }
    public long MessageCount { get; set; }
    public string PayloadEncoding { get; set; } = "string";
    public long PayloadBytes { get; set; }
    public string RawPayload { get; set; } = string.Empty;
    public string FormattedPayload { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
}

// --- RabbitMQ HTTP Management API DTOs ---

public class RabbitOverviewDto
{
    [JsonPropertyName("rabbitmq_version")]
    public string? RabbitMqVersion { get; set; }

    [JsonPropertyName("erlang_version")]
    public string? ErlangVersion { get; set; }

    [JsonPropertyName("message_stats")]
    public RabbitMessageStatsDto? MessageStats { get; set; }

    [JsonPropertyName("object_totals")]
    public RabbitObjectTotalsDto? ObjectTotals { get; set; }
}

public class RabbitObjectTotalsDto
{
    [JsonPropertyName("queues")]
    public int Queues { get; set; }

    [JsonPropertyName("consumers")]
    public int Consumers { get; set; }

    [JsonPropertyName("connections")]
    public int Connections { get; set; }

    [JsonPropertyName("channels")]
    public int Channels { get; set; }
}

public class RabbitQueueDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("vhost")]
    public string? Vhost { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("messages")]
    public long Messages { get; set; }

    [JsonPropertyName("messages_ready")]
    public long MessagesReady { get; set; }

    [JsonPropertyName("messages_unacknowledged")]
    public long MessagesUnacknowledged { get; set; }

    [JsonPropertyName("consumers")]
    public int Consumers { get; set; }

    [JsonPropertyName("message_stats")]
    public RabbitMessageStatsDto? MessageStats { get; set; }
}

public class RabbitMessageStatsDto
{
    [JsonPropertyName("publish_details")]
    public RabbitRateDetailDto? PublishDetails { get; set; }

    [JsonPropertyName("deliver_get_details")]
    public RabbitRateDetailDto? DeliverGetDetails { get; set; }
}

public class RabbitRateDetailDto
{
    [JsonPropertyName("rate")]
    public double Rate { get; set; }
}

public class RabbitPeekRequestDto
{
    [JsonPropertyName("count")]
    public int Count { get; set; } = 5;

    [JsonPropertyName("ackmode")]
    public string AckMode { get; set; } = "ack_requeue_true";

    [JsonPropertyName("encoding")]
    public string Encoding { get; set; } = "auto";

    [JsonPropertyName("truncate")]
    public int Truncate { get; set; } = 50000;
}

public class RabbitPeekMessageDto
{
    [JsonPropertyName("payload_bytes")]
    public long PayloadBytes { get; set; }

    [JsonPropertyName("redelivered")]
    public bool Redelivered { get; set; }

    [JsonPropertyName("exchange")]
    public string? Exchange { get; set; }

    [JsonPropertyName("routing_key")]
    public string? RoutingKey { get; set; }

    [JsonPropertyName("message_count")]
    public long MessageCount { get; set; }

    [JsonPropertyName("payload")]
    public string? Payload { get; set; }

    [JsonPropertyName("payload_encoding")]
    public string? PayloadEncoding { get; set; }

    [JsonPropertyName("properties")]
    public RabbitMessagePropertiesDto? Properties { get; set; }
}

public class RabbitMessagePropertiesDto
{
    [JsonPropertyName("content_type")]
    public string? ContentType { get; set; }

    [JsonPropertyName("timestamp")]
    public long? Timestamp { get; set; }
}

public class ServerPollResult
{
    public Guid ServerId { get; set; }
    public bool IsOnline { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string RabbitMqVersion { get; set; } = "-";
    public string ErlangVersion { get; set; } = "-";
    public double GlobalPublishRate { get; set; }
    public double GlobalDeliverRate { get; set; }
    public List<QueueItem> Queues { get; set; } = [];
    public DateTime CheckedAt { get; set; } = DateTime.Now;
}

