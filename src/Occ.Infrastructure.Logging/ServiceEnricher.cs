using Serilog.Core;
using Serilog.Events;

namespace Occ.Infrastructure.Logging;

internal sealed class ServiceEnricher : ILogEventEnricher
{
    private readonly string _serviceName;
    private readonly string _environment;

    public ServiceEnricher(string serviceName, string environment)
    {
        _serviceName = serviceName;
        _environment = environment;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("service.name", _serviceName));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("service.environment", _environment));
    }
}