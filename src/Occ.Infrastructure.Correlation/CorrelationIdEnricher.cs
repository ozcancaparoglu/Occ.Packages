using Serilog.Core;
using Serilog.Events;

namespace Occ.Infrastructure.Correlation;

public sealed class CorrelationIdEnricher : ILogEventEnricher
{
    private readonly ICorrelationAccessor _correlationAccessor;

    public CorrelationIdEnricher(ICorrelationAccessor correlationAccessor)
    {
        _correlationAccessor = correlationAccessor;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var correlationId = _correlationAccessor.CorrelationId;
        var property = propertyFactory.CreateProperty(CorrelationAccessor.LogPropertyName, correlationId);
        logEvent.AddPropertyIfAbsent(property);
    }
}