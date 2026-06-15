using Microsoft.AspNetCore.Http;
using Occ.Infrastructure.Correlation;

namespace Occ.AspNetCore.Middleware;

public sealed class RequestContextLoggingMiddleware(
    RequestDelegate next,
    ICorrelationAccessor correlationAccessor)
{
    internal const string CorrelationIdHeaderName = "X-Correlation-Id";

    public async Task Invoke(HttpContext context)
    {
        var correlationId = GetOrCreateCorrelationId(context);
        context.Response.Headers[CorrelationIdHeaderName] = correlationId;

        using (correlationAccessor.BeginScope(correlationId))
        {
            await next.Invoke(context);
        }
    }

    private string GetOrCreateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationIdHeaderName, out var value)
            && !string.IsNullOrWhiteSpace(value))
        {
            return value.ToString();
        }

        var newId = correlationAccessor.CorrelationId;
        context.Request.Headers[CorrelationIdHeaderName] = newId;
        return newId;
    }
}