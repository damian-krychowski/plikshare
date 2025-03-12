using PlikShare.Core.Utils;
using PlikShare.Users.Middleware;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using Serilog;

namespace PlikShare.Workspaces.Validation;

public class ValidateWorkspaceFilter : IEndpointFilter
{
    public const string WorkspaceMembershipContext = "workspace-membership-context";

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var workspaceExternalIdStr = context.HttpContext.Request.RouteValues["workspaceExternalId"]?.ToString();

        if (string.IsNullOrWhiteSpace(workspaceExternalIdStr))
            return HttpErrors.Workspace.MissingExternalId();

        if (!WorkspaceExtId.TryParse(workspaceExternalIdStr, null, out var workspaceExternalId))
            return HttpErrors.Workspace.BrokenExternalId(workspaceExternalIdStr);

        var user = context.HttpContext.GetUserContext();
        var workspaceMembershipCache = context.HttpContext.RequestServices.GetRequiredService<WorkspaceMembershipCache>();

        var workspaceMembershipContext = await workspaceMembershipCache.TryGetWorkspaceMembership(
            workspaceExternalId: workspaceExternalId,
            memberId: user.Id,
            cancellationToken: context.HttpContext.RequestAborted);

        if (workspaceMembershipContext is null || !workspaceMembershipContext.IsAvailableForUser)
        {
            Log.Warning(
                "User was trying to access Workspace '{WorkspaceExternalId}' which he doesn't have rights for",
                workspaceExternalId);

            return HttpErrors.Workspace.NotFound(workspaceExternalId);
        }

        context.HttpContext.Items[WorkspaceMembershipContext] = workspaceMembershipContext;
        return await next(context);
    }
}