using Microsoft.Extensions.DependencyInjection;
using Occ.AspNetCore.Http;

namespace Occ.AspNetCore.Extensions;

public static class HttpClientExtensions
{
    public static IHttpClientBuilder AddCorrelationIdPropagation(this IHttpClientBuilder builder)
        => builder.AddHttpMessageHandler<CorrelationIdDelegatingHandler>();
}