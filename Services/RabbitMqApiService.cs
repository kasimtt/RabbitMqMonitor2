using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using RabbitMqMonitor.Models;

namespace RabbitMqMonitor.Services;

/// <summary>
/// Communicates with RabbitMQ's Management HTTP API using a pooled SocketsHttpHandler
/// with short timeouts so offline servers never block or freeze the UI.
/// </summary>
public sealed class RabbitMqApiService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly CredentialEncryptionService _encryptionService;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public RabbitMqApiService(CredentialEncryptionService encryptionService)
    {
        _encryptionService = encryptionService;

        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),
            MaxConnectionsPerServer = 10,
            ConnectTimeout = TimeSpan.FromSeconds(3)
        };

        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(3.5)
        };
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<ServerPollResult> PollServerAsync(ServerProfile server, CancellationToken cancellationToken = default)
    {
        var result = new ServerPollResult
        {
            ServerId = server.Id,
            CheckedAt = DateTime.Now
        };

        string baseUrl = server.HostUrl.TrimEnd('/');
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            result.IsOnline = false;
            result.StatusText = "OFFLINE / INVALID URL";
            result.ErrorMessage = $"Invalid Host URL: '{server.HostUrl}'";
            return result;
        }

        string password = _encryptionService.Decrypt(server.EncryptedPassword);
        string authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{server.Username}:{password}"));

        try
        {
            // 1. Poll /api/overview
            string overviewUrl = $"{baseUrl}/api/overview";
            using var overviewReq = new HttpRequestMessage(HttpMethod.Get, overviewUrl);
            overviewReq.Headers.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

            using var overviewResp = await _httpClient.SendAsync(
                overviewReq,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);

            if (!overviewResp.IsSuccessStatusCode)
            {
                result.IsOnline = false;
                result.StatusText = FormatHttpErrorStatus(overviewResp.StatusCode);
                result.ErrorMessage = $"HTTP {(int)overviewResp.StatusCode} ({overviewResp.ReasonPhrase}) on /api/overview";
                return result;
            }

            await using var overviewStream = await overviewResp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var overview = await JsonSerializer.DeserializeAsync<RabbitOverviewDto>(overviewStream, JsonOptions, cancellationToken).ConfigureAwait(false);

            result.RabbitMqVersion = overview?.RabbitMqVersion ?? "Unknown";
            result.ErlangVersion = overview?.ErlangVersion ?? "Unknown";
            result.GlobalPublishRate = overview?.MessageStats?.PublishDetails?.Rate ?? 0.0;
            result.GlobalDeliverRate = overview?.MessageStats?.DeliverGetDetails?.Rate ?? 0.0;

            // 2. Poll /api/queues or /api/queues/{vhost}
            // Note: If VirtualHost is "*" or empty, fetch all vhosts; otherwise fetch specified vhost (or all if "/")
            string queuesUrl;
            string trimmedVhost = server.VirtualHost.Trim();
            if (string.IsNullOrEmpty(trimmedVhost) || trimmedVhost == "*")
            {
                queuesUrl = $"{baseUrl}/api/queues";
            }
            else
            {
                string encodedVhost = Uri.EscapeDataString(trimmedVhost);
                queuesUrl = $"{baseUrl}/api/queues/{encodedVhost}";
            }

            using var queuesReq = new HttpRequestMessage(HttpMethod.Get, queuesUrl);
            queuesReq.Headers.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

            using var queuesResp = await _httpClient.SendAsync(
                queuesReq,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);

            if (!queuesResp.IsSuccessStatusCode)
            {
                result.IsOnline = false;
                result.StatusText = FormatHttpErrorStatus(queuesResp.StatusCode);
                result.ErrorMessage = $"HTTP {(int)queuesResp.StatusCode} ({queuesResp.ReasonPhrase}) on {queuesUrl}";
                return result;
            }

            await using var queuesStream = await queuesResp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var queueDtos = await JsonSerializer.DeserializeAsync<List<RabbitQueueDto>>(queuesStream, JsonOptions, cancellationToken).ConfigureAwait(false)
                            ?? [];

            var mappedQueues = new List<QueueItem>(queueDtos.Count);
            foreach (var q in queueDtos)
            {
                mappedQueues.Add(new QueueItem
                {
                    ServerId = server.Id,
                    ServerName = server.Name,
                    Name = q.Name ?? "(unnamed)",
                    VirtualHost = q.Vhost ?? "/",
                    State = string.IsNullOrWhiteSpace(q.State) ? "unknown" : q.State,
                    TotalMessages = q.Messages,
                    ReadyMessages = q.MessagesReady,
                    UnackedMessages = q.MessagesUnacknowledged,
                    Consumers = q.Consumers,
                    PublishRate = q.MessageStats?.PublishDetails?.Rate ?? 0.0,
                    DeliverRate = q.MessageStats?.DeliverGetDetails?.Rate ?? 0.0,
                    AlertThreshold = server.QueueAlertThreshold
                });
            }

            result.IsOnline = true;
            result.StatusText = "ONLINE";
            result.ErrorMessage = string.Empty;
            result.Queues = mappedQueues;
            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            result.IsOnline = false;
            result.StatusText = "OFFLINE / TIMEOUT";
            result.ErrorMessage = "Connection timed out (> 3.5s)";
            return result;
        }
        catch (HttpRequestException ex)
        {
            result.IsOnline = false;
            if (ex.InnerException is SocketException sockEx)
            {
                result.StatusText = sockEx.SocketErrorCode switch
                {
                    SocketError.ConnectionRefused => "OFFLINE / CONNECTION REFUSED",
                    SocketError.HostNotFound => "OFFLINE / HOST NOT FOUND",
                    SocketError.TimedOut => "OFFLINE / TIMEOUT",
                    SocketError.NetworkUnreachable => "OFFLINE / NETWORK UNREACHABLE",
                    _ => $"OFFLINE / {sockEx.SocketErrorCode.ToString().ToUpperInvariant()}"
                };
                result.ErrorMessage = sockEx.Message;
            }
            else
            {
                result.StatusText = "OFFLINE / UNREACHABLE";
                result.ErrorMessage = ex.Message;
            }
            return result;
        }
        catch (Exception ex)
        {
            result.IsOnline = false;
            result.StatusText = "OFFLINE / ERROR";
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    /// <summary>
    /// Safely peeks up to <paramref name="count"/> messages from a queue using
    /// ackmode = "ack_requeue_true" so messages are NEVER removed from the queue.
    /// </summary>
    public async Task<(bool Success, string Error, List<PeekedMessage> Messages)> PeekMessagesAsync(
        ServerProfile server,
        string vhost,
        string queueName,
        int count = 5,
        CancellationToken cancellationToken = default)
    {
        try
        {
            string baseUrl = server.HostUrl.TrimEnd('/');
            string encodedVhost = Uri.EscapeDataString(string.IsNullOrEmpty(vhost) ? "/" : vhost);
            string encodedQueue = Uri.EscapeDataString(queueName);
            string url = $"{baseUrl}/api/queues/{encodedVhost}/{encodedQueue}/get";

            string password = _encryptionService.Decrypt(server.EncryptedPassword);
            string authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{server.Username}:{password}"));

            var payloadObj = new RabbitPeekRequestDto
            {
                Count = count,
                AckMode = "ack_requeue_true",
                Encoding = "auto",
                Truncate = 50000
            };

            string requestJson = JsonSerializer.Serialize(payloadObj);
            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

            using var resp = await _httpClient.SendAsync(req, cancellationToken).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                return (false, $"HTTP {(int)resp.StatusCode} ({resp.ReasonPhrase})", []);
            }

            string body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var dtos = JsonSerializer.Deserialize<List<RabbitPeekMessageDto>>(body, JsonOptions) ?? [];

            var list = new List<PeekedMessage>(dtos.Count);
            int idx = 1;
            foreach (var dto in dtos)
            {
                string raw = dto.Payload ?? string.Empty;
                string formatted = TryFormatJson(raw);
                string timestampStr = dto.Properties?.Timestamp is long ts
                    ? DateTimeOffset.FromUnixTimeSeconds(ts).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
                    : "-";

                list.Add(new PeekedMessage
                {
                    Index = idx++,
                    Exchange = string.IsNullOrEmpty(dto.Exchange) ? "(default)" : dto.Exchange,
                    RoutingKey = dto.RoutingKey ?? string.Empty,
                    Redelivered = dto.Redelivered,
                    MessageCount = dto.MessageCount,
                    PayloadBytes = dto.PayloadBytes,
                    PayloadEncoding = dto.PayloadEncoding ?? "string",
                    ContentType = dto.Properties?.ContentType ?? "text/plain",
                    Timestamp = timestampStr,
                    RawPayload = raw,
                    FormattedPayload = formatted
                });
            }

            return (true, string.Empty, list);
        }
        catch (Exception ex)
        {
            return (false, ex.Message, []);
        }
    }

    private static string FormatHttpErrorStatus(HttpStatusCode code)
    {
        return code switch
        {
            HttpStatusCode.Unauthorized => "OFFLINE / 401 UNAUTHORIZED",
            HttpStatusCode.Forbidden => "OFFLINE / 403 FORBIDDEN",
            HttpStatusCode.NotFound => "OFFLINE / 404 VHOST NOT FOUND",
            HttpStatusCode.BadGateway => "OFFLINE / 502 BAD GATEWAY",
            HttpStatusCode.ServiceUnavailable => "OFFLINE / 503 UNAVAILABLE",
            _ => $"OFFLINE / HTTP {(int)code}"
        };
    }

    private static string TryFormatJson(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "(empty payload)";

        string trimmed = input.Trim();
        if ((trimmed.StartsWith('{') && trimmed.EndsWith('}')) ||
            (trimmed.StartsWith('[') && trimmed.EndsWith(']')))
        {
            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
            }
            catch
            {
                return input;
            }
        }

        return input;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

