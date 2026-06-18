using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PlikShare.Agents.Middleware;
using PlikShare.Agents.Operations;
using PlikShare.Agents.Tools;
using PlikShare.Core.Clock;
using PlikShare.Core.Utils;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;

namespace PlikShare.Mcp.BulkDelete;

[McpServerToolType]
public class BulkDeleteTool
{
    [McpServerTool(Name = AgentToolNames.BulkDelete)]
    [Description("Deletes files and/or folders in a workspace the agent can access. Each listed folder is " +
                 "deleted together with everything inside it (all subfolders and files), like 'rm -rf'. " +
                 "Provide at least one id in folderExternalIds or fileExternalIds. If this tool requires " +
                 "approval the call returns status 'waits_for_approval' with an approvalRequestId — poll " +
                 "check_approvals and, once approved, call execute_operation to run it.")]
    public static async Task<AgentToolResponse> Execute(
        IHttpContextAccessor httpContextAccessor,
        WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
        AgentWorkspaceToolOverrideReader workspaceToolOverrideReader,
        BulkDeleteAgentOperation bulkDeleteOperation,
        AgentOperationLedger operationLedger,
        AgentOperationsOptions operationsOptions,
        IClock clock,
        [Description("External id of the workspace.")]
        string workspaceExternalId,
        [Description("External ids of folders to delete, together with their entire contents. Optional if files are given.")]
        string[]? folderExternalIds = null,
        [Description("External ids of files to delete. Optional if folders are given.")]
        string[]? fileExternalIds = null,
        CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext!;
        var agent = await httpContext.GetAgentContext();

        var folders = folderExternalIds ?? [];
        var files = fileExternalIds ?? [];

        if (folders.Length == 0 && files.Length == 0)
            throw new McpException("Provide at least one id in folderExternalIds or fileExternalIds.");

        // Validate access up front so we never queue an approval for something the agent can't do.
        var membership = await httpContext.GetWorkspaceAgentMembershipDetails(
            workspaceAgentMembershipCache,
            WorkspaceExtId.Parse(workspaceExternalId),
            cancellationToken);

        membership.Workspace.ThrowIfFullyEncrypted();

        var parameters = new BulkDeleteParams
        {
            WorkspaceExternalId = workspaceExternalId,
            FolderExternalIds = folders,
            FileExternalIds = files
        };

        // Cascade the agent's config for this workspace: per-workspace override → global → catalog
        // default. The workspace override governs both whether the tool is usable here and whether
        // it needs approval — evaluating without it would ignore the admin's per-workspace settings.
        var definition = AgentToolCatalog.TryGet(AgentToolNames.BulkDelete)!;

        var workspaceOverride = workspaceToolOverrideReader.TryGet(
            agentId: agent.Id,
            workspaceId: membership.Workspace.Id,
            toolName: AgentToolNames.BulkDelete);

        var effective = AgentToolCatalog.Resolve(
            agent,
            definition,
            workspaceOverride);

        if (!effective.IsUsable)
            throw new McpException("The bulk_delete tool is not enabled for this workspace.");

        if (effective.RequiresApproval)
        {
            var expiresAt = clock.UtcNow.AddHours(
                operationsOptions.ApprovalWindowHours);

            var operationId = await operationLedger.CreatePending(
                agentId: agent.Id,
                workspaceId: membership.Workspace.Id,
                toolName: AgentToolNames.BulkDelete,
                paramsJson: Json.Serialize(parameters),
                expiresAt: expiresAt,
                cancellationToken: cancellationToken);

            return AgentToolResponse.WaitsForApproval(
                approvalRequestId: operationId.Value,
                expiresAt: expiresAt);
        }

        var result = await bulkDeleteOperation.Execute(
            httpContext,
            parameters,             
            cancellationToken);
            
        return AgentToolResponse.Executed(
            result);
    }
}
