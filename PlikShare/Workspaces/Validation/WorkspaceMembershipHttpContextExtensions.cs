using PlikShare.Workspaces.Cache;

namespace PlikShare.Workspaces.Validation;

public static class WorkspaceMembershipHttpContextExtensions
{
    public static WorkspaceMembershipContext GetWorkspaceMembershipDetails(this HttpContext httpContext)
    {
        var workspace =  httpContext.Items[ValidateWorkspaceFilter.WorkspaceMembershipContext];

        if (workspace is not WorkspaceMembershipContext context)
        {
            throw new InvalidOperationException(
                $"Cannot extract WorkspaceContext from HttpContext.");
        }

        return context;
    }
}