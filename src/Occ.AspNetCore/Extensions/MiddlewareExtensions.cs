using Microsoft.AspNetCore.Builder;
using Occ.AspNetCore.Middleware;

namespace Occ.AspNetCore.Extensions;

public static class MiddlewareExtensions
{
    public static IApplicationBuilder UseRequestContextLogging(this IApplicationBuilder app)
    {
        app.UseMiddleware<RequestContextLoggingMiddleware>();
        return app;
    }
}