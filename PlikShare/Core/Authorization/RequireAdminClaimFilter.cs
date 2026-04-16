using System.Security.Claims;

namespace PlikShare.Core.Authorization;

public class RequireAdminClaimEndpointFilter(Claim requiredClaim) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var user = context.HttpContext.User;

        // AppOwner can do whatever they want
        if (user.IsAppOwner())
            return await next(context);

        if (!user.IsInRole(Roles.Admin))
            return Results.Forbid();

        var hasRequiredClaim = user.Claims.Any(
            c => c.Type == requiredClaim.Type && c.Value == requiredClaim.Value);

        if (!hasRequiredClaim)
            return Results.Forbid();

        return await next(context);
    }
}

public class RequireAdminPermissionEndpointFilter(string permission)
    : RequireAdminClaimEndpointFilter(new Claim("permission", permission));