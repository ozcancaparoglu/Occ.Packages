using Elastic.Channels;
using Elastic.Ingest.Elasticsearch;
using Elastic.Ingest.Elasticsearch.DataStreams;
using Elastic.Serilog.Sinks;
using Elastic.Transport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Occ.Infrastructure.Logging.Elasticsearch;

public static class ElasticsearchLoggingExtensions
{
    /// <summary>
    /// Adds the Elasticsearch sink to the Serilog configuration.
    /// Reads options from the "Occ:Elasticsearch" config section.
    /// No-ops if the URI is not configured.
    ///
    /// Usage:
    ///   builder.Host.UseOccLogging((cfg, ctx) => cfg.WriteToElasticsearch(ctx));
    /// </summary>
    public static LoggerConfiguration WriteToElasticsearch(
        this LoggerConfiguration configuration,
        HostBuilderContext context)
    {
        var options = context.Configuration
            .GetSection(ElasticOptions.SectionName)
            .Get<ElasticOptions>();

        if (options is null || string.IsNullOrWhiteSpace(options.Uri))
            return configuration;

        var environment = context.HostingEnvironment.EnvironmentName.ToLowerInvariant();
        var serviceName = options.ServiceName
            ?? context.HostingEnvironment.ApplicationName.ToLowerInvariant();

        var dataStream = new DataStreamName("logs", $"occ.{serviceName}", environment);
        var nodes = new[] { new Uri(options.Uri) };

        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            configuration.WriteTo.Elasticsearch(
                nodes,
                opts =>
                {
                    opts.DataStream = dataStream;
                    opts.BootstrapMethod = BootstrapMethod.None;
                    opts.ConfigureChannel = channelOpts =>
                        channelOpts.BufferOptions = new BufferOptions { ExportMaxConcurrency = 2 };
                },
                transport => transport.Authentication(new ApiKey(options.ApiKey)));
        }
        else
        {
            configuration.WriteTo.Elasticsearch(
                nodes,
                opts =>
                {
                    opts.DataStream = dataStream;
                    opts.BootstrapMethod = BootstrapMethod.None;
                    opts.ConfigureChannel = channelOpts =>
                        channelOpts.BufferOptions = new BufferOptions { ExportMaxConcurrency = 2 };
                });
        }

        return configuration;
    }
}