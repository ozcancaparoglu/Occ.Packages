using Occ.AspNetCore.Middleware;
using Occ.Infrastructure.Correlation;

namespace Occ.AspNetCore.Http;

public sealed class CorrelationIdDelegatingHandler(ICorrelationAccessor correlationAccessor)
    : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var correlationId = correlationAccessor.CorrelationId;

        if (!request.Headers.Contains(RequestContextLoggingMiddleware.CorrelationIdHeaderName))
        {
            request.Headers.TryAddWithoutValidation(
                RequestContextLoggingMiddleware.CorrelationIdHeaderName,
                correlationId);
        }

        return base.SendAsync(request, cancellationToken);
    }
}