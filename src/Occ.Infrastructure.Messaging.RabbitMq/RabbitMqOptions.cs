namespace Occ.Infrastructure.Messaging.RabbitMq;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    // Connection settings (shared by publisher and consumer)
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";

    // Publisher settings
    public string Exchange { get; set; } = string.Empty;
    public string ExchangeType { get; set; } = RabbitMQ.Client.ExchangeType.Direct;
    public string RoutingKey { get; set; } = string.Empty;

    // Consumer settings
    public string Queue { get; set; } = string.Empty;

    // Retry topology
    public string RetryExchange { get; set; } = string.Empty;
    public string RetryQueue { get; set; } = string.Empty;
    public int RetryDelayMs { get; set; } = 30_000;

    // Dead letter topology
    public string DeadLetterExchange { get; set; } = string.Empty;
    public string DeadLetterQueue { get; set; } = string.Empty;
    public string DeadLetterRoutingKey { get; set; } = string.Empty;

    // Retry behavior
    public int MaxRetryCount { get; set; } = 5;

    // Auto-declare exchanges and queues on startup
    public bool AutoCreateTopology { get; set; } = true;
}