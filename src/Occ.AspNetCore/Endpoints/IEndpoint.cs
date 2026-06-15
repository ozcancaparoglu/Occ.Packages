using Microsoft.AspNetCore.Routing;

namespace Occ.AspNetCore.Endpoints;

public interface IEndpoint
{
    void MapEndpoint(IEndpointRouteBuilder app);
}