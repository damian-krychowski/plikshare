using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PlikShare.Core.Authorization;

/// <summary>
/// The fact that user is an admin does not override the need for a claim
/// </summary>
/// <param name="claim"></param>
public class RequireAdminClaimFilter(Claim claim) : IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context
            .HttpContext
            .User;
        
        //AppOwner can do whatever he wants
        if(user.GetIsAppOwner())
            return;
        
        var hasAdminClaim = user.IsInRole(Roles.Admin)
            && user.Claims.Any(c => c.Type == claim.Type && c.Value == claim.Value);

        if (hasAdminClaim) 
            return;
        
        context.Result = new ForbidResult();
    }
}

public class RequireAdminClaimEndpointFilter(Claim requiredClaim) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var user = context.HttpContext.User;

        // AppOwner can do whatever they want
        if (user.GetIsAppOwner())
        {
            return await next(context);
        }

        var hasAdminClaim = user.IsInRole(Roles.Admin)
                            && user.Claims.Any(c => c.Type == requiredClaim.Type && c.Value == requiredClaim.Value);

        if (hasAdminClaim)
        {
            return await next(context);
        }

        return Results.Forbid();
    }
}

public class RequireAdminPermissionEndpointFilter(string permission)
    : RequireAdminClaimEndpointFilter(new Claim("permission", permission));