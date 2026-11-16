using System.Text.Json;
using System.Text.Json.Serialization;

namespace RabbitMqMonitor.Models;

public class NodeItem
{
    public Guid ServerId { get; set; }
    public string ServerName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsRunning { get; set; } = true;

    public long FdUsed { get; set; }
    public long FdTotal { get; set; }
    public string FdAvailableText => $"{FdTotal} available";

    public long ProcUsed { get; set; }
    public long ProcTotal { get; set; }
    public string ProcAvailableText => $"{ProcTotal} available";

    public long MemUsedBytes { get; set; }
    public long MemLimitBytes { get; set; }
    public bool MemAlarm { get; set; }
    public string MemUsedDisplay => FormatBinaryBytes(MemUsedBytes);
    public string MemWatermarkText => $"{FormatBinaryBytes(MemLimitBytes)} high watermark";

    public long DiskFreeBytes { get; set; }
    public long DiskFreeLimitBytes { get; set; }
    public bool DiskFreeAlarm { get; set; }
    public string DiskFreeDisplay => FormatBinaryBytes(DiskFreeBytes);
    public string DiskWatermarkText => $"{FormatBinaryBytes(DiskFreeLimitBytes)} low watermark";

    public long UptimeMilliseconds { get; set; }
    public string UptimeDisplay => FormatUptime(UptimeMilliseconds);

    public int Cores { get; set; }
    public string RatesMode { get; set; } = "basic";
    public string MemStrategy { get; set; } = "rss";

    public static string FormatBinaryBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";
        const double kib = 1024.0;
        const double mib = kib * 1024.0;
        const double gib = mib * 1024.0;
        const double tib = gib * 1024.0;

        if (bytes >= tib)
            return $"{bytes / tib:0.#} TiB";
        if (bytes >= gib)
        {
            double val = bytes / gib;
            return val >= 10 ? $"{val:0} GiB" : $"{val:0.#} GiB";
        }
        if (bytes >= mib)
        {
            double val = bytes / mib;
            return val >= 10 ? $"{val:0} MiB" : $"{val:0.#} MiB";
        }
        if (bytes >= kib)
            return $"{bytes / kib:0} KiB";
        return $"{bytes} B";
    }

    public static string FormatUptime(long uptimeMs)
    {
        if (uptimeMs <= 0) return "-";
        var ts = TimeSpan.FromMilliseconds(uptimeMs);
        if (ts.TotalDays >= 1)
            return $"{(int)ts.TotalDays}d {ts.Hours}h";
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        if (ts.TotalMinutes >= 1)
            return $"{(int)ts.TotalMinutes}m {ts.Seconds}s";
        return $"{ts.Seconds}s";
    }
}

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

public class RabbitNodeDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("running")]
    public bool? Running { get; set; }

    [JsonPropertyName("fd_used")]
    public JsonElement? FdUsed { get; set; }

    [JsonPropertyName("fd_total")]
    public JsonElement? FdTotal { get; set; }

    [JsonPropertyName("proc_used")]
    public JsonElement? ProcUsed { get; set; }

    [JsonPropertyName("proc_total")]
    public JsonElement? ProcTotal { get; set; }

    [JsonPropertyName("mem_used")]
    public JsonElement? MemUsed { get; set; }

    [JsonPropertyName("mem_limit")]
    public JsonElement? MemLimit { get; set; }

    [JsonPropertyName("mem_alarm")]
    public bool? MemAlarm { get; set; }

    [JsonPropertyName("disk_free")]
    public JsonElement? DiskFree { get; set; }

    [JsonPropertyName("disk_free_limit")]
    public JsonElement? DiskFreeLimit { get; set; }

    [JsonPropertyName("disk_free_alarm")]
    public bool? DiskFreeAlarm { get; set; }

    [JsonPropertyName("uptime")]
    public JsonElement? Uptime { get; set; }

    [JsonPropertyName("processors")]
    public JsonElement? Processors { get; set; }

    [JsonPropertyName("rates_mode")]
    public string? RatesMode { get; set; }

    [JsonPropertyName("mem_calculation_strategy")]
    public string? MemCalculationStrategy { get; set; }
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
    public List<NodeItem> Nodes { get; set; } = [];
    public DateTime CheckedAt { get; set; } = DateTime.Now;
}

