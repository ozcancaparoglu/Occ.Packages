namespace Occ.Infrastructure.Logging.Elasticsearch;

public sealed class ElasticOptions
{
    public const string SectionName = "Occ:Elasticsearch";

    /// <summary>
    /// Elasticsearch cluster URI (e.g. "https://es-prod.onspay.internal:9200").
    /// </summary>
    public string Uri { get; set; } = null!;

    /// <summary>
    /// API key for authentication. Source from Vault or a secrets manager.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Overrides the service name used in data stream naming
    /// (logs-occ.{ServiceName}-{environment}).
    /// Defaults to the hosting application name.
    /// </summary>
    public string? ServiceName { get; set; }
}