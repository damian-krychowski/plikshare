using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PlikShare.Core.Authorization;

public class RequireClaimFilter(Claim claim) : IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context
            .HttpContext
            .User;
        
        //admin role overrides a need for any permission
        if(user.IsInRole(Roles.Admin))
            return;
        
        var hasClaim = user
            .Claims
            .Any(c => c.Type == claim.Type && c.Value == claim.Value);

        if (hasClaim) 
            return;
        
        context.Result = new ForbidResult();
    }
}

public class RequireClaimEndpointFilter(Claim claim) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var user = context.HttpContext.User;

        // Admin role overrides a need for any permission
        if (user.IsInRole(Roles.Admin))
            return await next(context);

        var hasClaim = user.Claims.Any(c => c.Type == claim.Type && c.Value == claim.Value);
        if (hasClaim)
            return await next(context);

        return new ForbidResult();
    }
}

public class RequirePermissionEndpointFilter(string permission)
    : RequireClaimEndpointFilter(new Claim("permission", permission));