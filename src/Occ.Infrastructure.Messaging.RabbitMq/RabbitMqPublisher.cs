using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Occ.Infrastructure.Correlation;
using Occ.SharedKernal;
using RabbitMQ.Client;

namespace Occ.Infrastructure.Messaging.RabbitMq;

public sealed class RabbitMqPublisher : IMessagePublisher, IAsyncDisposable
{
    private const string CorrelationIdHeader = "x-correlation-id";

    private readonly IOptionsMonitor<RabbitMqOptions> _optionsMonitor;
    private readonly ICorrelationAccessor _correlationAccessor;
    private readonly ILogger<RabbitMqPublisher> _logger;
    private RabbitMqOptions Options => _optionsMonitor.CurrentValue;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private IConnection? _connection;
    private IChannel? _channel;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public RabbitMqPublisher(
        IOptionsMonitor<RabbitMqOptions> options,
        ICorrelationAccessor correlationAccessor,
        ILogger<RabbitMqPublisher> logger)
    {
        _optionsMonitor = options;
        _correlationAccessor = correlationAccessor;
        _logger = logger;
    }

    public async Task PublishAsync<T>(
        T message,
        string messageType,
        string? messageId = null,
        string? correlationId = null,
        string? routingKey = null,
        CancellationToken cancellationToken = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageType);

        await EnsureInitializedAsync(cancellationToken);

        // MessageId is critical - consumer uses it as the inbox idempotency key.
        // If caller doesn't supply one, generate a stable GUID.
        messageId ??= Guid.NewGuid().ToString("N");

        var payload = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);

        await PublishBytesAsync(payload, messageType, messageId, correlationId, routingKey, cancellationToken);
    }

    public async Task PublishRawAsync(
        string payload,
        string messageType,
        string? messageId = null,
        string? correlationId = null,
        string? routingKey = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageType);

        await EnsureInitializedAsync(cancellationToken);

        messageId ??= Guid.NewGuid().ToString("N");

        var body = System.Text.Encoding.UTF8.GetBytes(payload);
        await PublishBytesAsync(body, messageType, messageId, correlationId, routingKey, cancellationToken);
    }

    private async Task PublishBytesAsync(
        ReadOnlyMemory<byte> body,
        string messageType,
        string messageId,
        string? correlationId,
        string? routingKey,
        CancellationToken cancellationToken)
    {
        // Use the ambient correlation ID from the accessor if not explicitly provided
        var effectiveCorrelationId = correlationId ?? _correlationAccessor.CorrelationId;

        var headers = new Dictionary<string, object?>
        {
            [CorrelationIdHeader] = effectiveCorrelationId
        };

        var props = new BasicProperties
        {
            MessageId = messageId,
            Type = messageType,
            ContentType = "application/json",
            ContentEncoding = "utf-8",
            DeliveryMode = DeliveryModes.Persistent,
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            Headers = headers
        };

        await _channel!.BasicPublishAsync(
            exchange: Options.Exchange,
            routingKey: routingKey ?? Options.RoutingKey,
            mandatory: false,
            basicProperties: props,
            body: body,
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Published message. MessageId: {MessageId}, Type: {MessageType}, RoutingKey: {RoutingKey}, CorrelationId: {CorrelationId}",
            messageId, messageType, routingKey ?? Options.RoutingKey, effectiveCorrelationId);
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true }) return;

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_channel is { IsOpen: true }) return;

            var factory = new ConnectionFactory
            {
                HostName = Options.Host,
                Port = Options.Port,
                UserName = Options.Username,
                Password = Options.Password,
                VirtualHost = Options.VirtualHost,
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
            };

            _connection = await factory.CreateConnectionAsync(cancellationToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

            _logger.LogInformation("Publisher channel opened for exchange {Exchange}", Options.Exchange);
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.CloseAsync();
            await _channel.DisposeAsync();
        }

        if (_connection is not null)
        {
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
        }

        _initLock.Dispose();
    }
}