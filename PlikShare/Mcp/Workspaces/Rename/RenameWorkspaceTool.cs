using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PlikShare.Agents.Tools;
using PlikShare.AuditLog;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using PlikShare.Workspaces.UpdateName;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.Workspaces.Rename;

[McpServerToolType]
public class RenameWorkspaceTool
{
    [McpServerTool(Name = AgentToolNames.RenameWorkspace)]
    [Description("Renames a workspace the agent has access to.")]
    public static async Task Execute(
        IHttpContextAccessor httpContextAccessor,
        WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
        WorkspaceCache workspaceCache,
        UpdateWorkspaceNameQuery updateWorkspaceNameQuery,
        AuditLogService auditLogService,
        [Description("External id of the workspace to rename.")]
        string workspaceExternalId,
        [Description("New name for the workspace.")]
        string name,
        CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext!;

        var membership = await httpContext.GetWorkspaceAgentMembershipDetails(
            workspaceAgentMembershipCache,
            WorkspaceExtId.Parse(workspaceExternalId),
            cancellationToken);

        var workspace = membership.Workspace;
        workspace.ThrowIfFullyEncrypted();

        var resultCode = await updateWorkspaceNameQuery.Execute(
            workspace: workspace,
            name: name,
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case UpdateWorkspaceNameQuery.ResultCode.Ok:
                await workspaceCache.InvalidateEntry(
                    workspace.ExternalId,
                    cancellationToken);

                await auditLogService.LogWithStorageContext(
                    storageExternalId: workspace.Storage.ExternalId,
                    buildEntry: storageRef => Audit.Workspace.NameUpdatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        storage: storageRef,
                        workspace: new Audit.WorkspaceRef
                        {
                            ExternalId = workspace.ExternalId,
                            Name = name
                        }),
                    cancellationToken);

                return;

            case UpdateWorkspaceNameQuery.ResultCode.NotFound:
                throw new McpException(
                    $"Workspace '{workspaceExternalId}' was not found.");

            default:
                throw new McpException(
                    $"Unexpected result while renaming workspace: {resultCode}.");
        }
    }
}
