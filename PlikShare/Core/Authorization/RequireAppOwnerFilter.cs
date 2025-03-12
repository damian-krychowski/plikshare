using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PlikShare.Core.Authorization;

public class RequireAppOwnerFilter() : IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context
            .HttpContext
            .User;
        
        if(user.GetIsAppOwner())
            return;
        
        context.Result = new ForbidResult();
    }
}

public class RequireAppOwnerEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var user = context.HttpContext.User;

        if (user.GetIsAppOwner())
        {
            return await next(context);
        }

        return Results.Forbid();
    }
}