using PlikShare.Boxes.Permissions;
using PlikShare.BoxLinks.Cache;
using PlikShare.BoxLinks.Validation;
using PlikShare.Core.Authorization;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;

namespace PlikShare.BoxExternalAccess.Authorization;

public class ValidateAccessCodeFilter(
    params BoxPermission[]? requiredPermissions) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var accessCodeStr = context.HttpContext.Request.RouteValues["accessCode"]?.ToString();

        if (string.IsNullOrWhiteSpace(accessCodeStr))
            return HttpErrors.Box.InvalidAccessCode();

        var boxLinkContext = await context
            .HttpContext
            .RequestServices
            .GetRequiredService<BoxLinkCache>()
            .TryGetBoxLink(
                accessCode: accessCodeStr,
                cancellationToken: context.HttpContext.RequestAborted);
        
        if (boxLinkContext is null 
            or {Box.IsBeingDeleted: true}
            or {Box.Workspace.IsBeingDeleted: true})
            return HttpErrors.Box.InvalidAccessCode();

        var boxAccess =  new BoxAccess(
            IsEnabled: boxLinkContext is { IsEnabled: true, Box.IsEnabled: true },
            Box: boxLinkContext.Box,
            Permissions: boxLinkContext.Permissions,
            UserIdentity: new BoxLinkSessionUserIdentity(
                BoxLinkSessionId: context.HttpContext.User.GetBoxLinkSessionIdOrThrow()));

        context.HttpContext.Items[BoxAccess.HttpContextName] = boxAccess;
        context.HttpContext.Items[ValidateBoxLinkFilter.BoxLinkContext] = boxLinkContext;
        
        if (HasAllRequiredPermissions(boxAccess))
        {
            return await next(context);
        }

        return TypedResults.StatusCode(
            StatusCodes.Status403Forbidden);
    }

    private bool HasAllRequiredPermissions(BoxAccess boxAccess)
    {
        if (requiredPermissions is null)
            return true;

        if (requiredPermissions.Length == 0)
            return true;
        
        if (boxAccess.IsOff)
            return false;
        
        return requiredPermissions.All(
            permission => boxAccess.Permissions.HasPermission(permission));
    }
}