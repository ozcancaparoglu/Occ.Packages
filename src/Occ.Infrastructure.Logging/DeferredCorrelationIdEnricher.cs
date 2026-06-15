using Occ.Infrastructure.Correlation;
using Serilog.Core;
using Serilog.Events;

namespace Occ.Infrastructure.Logging;

internal sealed class DeferredCorrelationIdEnricher : ILogEventEnricher
{
    private readonly IServiceProvider _serviceProvider;
    private ICorrelationAccessor? _correlationAccessor;
    private bool _resolved;

    public DeferredCorrelationIdEnricher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (!_resolved)
        {
            _correlationAccessor = _serviceProvider.GetService(typeof(ICorrelationAccessor)) as ICorrelationAccessor;
            _resolved = true;
        }

        if (_correlationAccessor is null)
        {
            return;
        }

        var correlationId = _correlationAccessor.CorrelationId;
        var property = propertyFactory.CreateProperty(CorrelationAccessor.LogPropertyName, correlationId);
        logEvent.AddPropertyIfAbsent(property);
    }
}
