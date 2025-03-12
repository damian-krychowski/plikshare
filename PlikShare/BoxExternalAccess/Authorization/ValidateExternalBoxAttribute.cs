using PlikShare.Boxes.Cache;
using PlikShare.Boxes.Id;
using PlikShare.Boxes.Permissions;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Users.Middleware;

namespace PlikShare.BoxExternalAccess.Authorization;

public class ValidateExternalBoxFilter(
    params BoxPermission[]? requiredPermissions) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var boxExternalIdStr = context.HttpContext.Request.RouteValues["boxExternalId"]?.ToString();

        if (string.IsNullOrWhiteSpace(boxExternalIdStr))
            return HttpErrors.Box.MissingExternalId();

        if (!BoxExtId.TryParse(boxExternalIdStr, null, out var boxExternalId))
            return HttpErrors.Box.InvalidExternalId(boxExternalIdStr);

        var user = context.HttpContext.GetUserContext();

        var boxMembershipContext = await context
            .HttpContext
            .RequestServices
            .GetRequiredService<BoxMembershipCache>()
            .TryGetBoxMembership(
                boxExternalId: boxExternalId,
                memberId: user.Id,
                cancellationToken: context.HttpContext.RequestAborted);

        if (boxMembershipContext is null
            or { WasInvitationAccepted: false }
            or { Box.IsBeingDeleted: true }
            or { Box.Workspace.IsBeingDeleted: true })
        {
            return HttpErrors.Box.NotFound(boxExternalId);
        }

        var boxAccess = new BoxAccess(
            IsEnabled: boxMembershipContext.Box.IsEnabled,
            Box: boxMembershipContext.Box,
            Permissions: boxMembershipContext.Permissions,
            UserIdentity: new UserIdentity(user.ExternalId));

        context.HttpContext.Items[BoxAccess.HttpContextName] = boxAccess;

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