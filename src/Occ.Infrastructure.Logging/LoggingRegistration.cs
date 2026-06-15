using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Occ.Infrastructure.Correlation;
using Serilog;
using Serilog.Events;

namespace Occ.Infrastructure.Logging;

public static class LoggingRegistration
{
    /// <summary>
    /// Configures Serilog with shared enrichers, default minimum levels, Console, and
    /// anything declared under the "Serilog" appsettings section (ReadFrom.Configuration).
    ///
    /// Pass <paramref name="configureSinks"/> to add extra sinks (e.g. Elasticsearch):
    ///   builder.Host.UseOccLogging((cfg, ctx) => cfg.WriteToElasticsearch(ctx));
    /// </summary>
    public static IHostBuilder UseOccLogging(
        this IHostBuilder builder,
        Action<LoggerConfiguration, HostBuilderContext>? configureSinks = null)
    {
        builder.ConfigureServices((_, services) =>
        {
            services.TryAddSingleton<ICorrelationAccessor, CorrelationAccessor>();
        });

        return builder.UseSerilog((context, services, configuration) =>
        {
            var environment = context.HostingEnvironment.EnvironmentName.ToLowerInvariant();
            var serviceName = context.HostingEnvironment.ApplicationName.ToLowerInvariant();

            configuration
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.With(new ServiceEnricher(serviceName, environment))
                .Enrich.With(new DeferredCorrelationIdEnricher(services))
                .ReadFrom.Configuration(context.Configuration);

            configureSinks?.Invoke(configuration, context);
        });
    }
}