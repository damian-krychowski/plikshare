using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PlikShare.Agents.Middleware;
using PlikShare.Agents.Operations;
using PlikShare.Agents.Tools;
using PlikShare.Core.Clock;
using PlikShare.Core.Utils;
using PlikShare.QuickShares;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;

namespace PlikShare.Mcp.ShareLinks.Create;

[McpServerToolType]
public class CreateShareLinkTool
{
    [McpServerTool(Name = AgentToolNames.CreateShareLink)]
    [Description("Creates a public link that shares the selected files and/or folders from a workspace the " +
                 "agent can access. Anyone with the link can download the shared items — no login required. " +
                 "Provide at least one id in fileExternalIds or folderExternalIds. Optional: expiresAt " +
                 "(ISO 8601), maxDownloads, password. Returns the share's external id and public URL. If this " +
                 "tool requires approval the call returns status 'waits_for_approval' with an approvalRequestId " +
                 "— poll check_approvals and, once approved, call execute_operation to run it.")]
    public static async Task<AgentToolResponse> Execute(
        IHttpContextAccessor httpContextAccessor,
        WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
        AgentWorkspaceToolOverrideReader workspaceToolOverrideReader,
        CreateShareLinkAgentOperation createShareLinkOperation,
        AgentOperationLedger operationLedger,
        AgentOperationsOptions operationsOptions,
        IClock clock,
        [Description("External id of the workspace.")]
        string workspaceExternalId,
        [Description("Name for the share link.")]
        string name,
        [Description("External ids of files to share. Optional if folders are given.")]
        string[]? fileExternalIds = null,
        [Description("External ids of folders to share. Optional if files are given.")]
        string[]? folderExternalIds = null,
        [Description("Optional external ids of files to carve out of the shared folders (the folder is shared but these files are not).")]
        string[]? excludedFileExternalIds = null,
        [Description("Optional external ids of subfolders to carve out of the shared folders.")]
        string[]? excludedFolderExternalIds = null,
        [Description("Optional ISO 8601 timestamp after which the link stops working.")]
        string? expiresAt = null,
        [Description("Optional maximum number of downloads.")]
        int? maxDownloads = null,
        [Description("Optional password required to open the link.")]
        string? password = null,
        CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext!;
        var agent = await httpContext.GetAgentContext();

        var membership = await httpContext.GetWorkspaceAgentMembershipDetails(
            workspaceAgentMembershipCache,
            WorkspaceExtId.Parse(workspaceExternalId),
            cancellationToken);

        var workspace = membership.Workspace;
        workspace.ThrowIfFullyEncrypted();

        var trimmedName = (name ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(trimmedName))
            throw new McpException("A name for the share link is required.");

        var files = fileExternalIds ?? [];
        var folders = folderExternalIds ?? [];
        var excludedFiles = excludedFileExternalIds ?? [];
        var excludedFolders = excludedFolderExternalIds ?? [];

        if (files.Length == 0 && folders.Length == 0)
            throw new McpException("Provide at least one id in fileExternalIds or folderExternalIds.");

        DateTimeOffset? expires = null;

        if (!string.IsNullOrWhiteSpace(expiresAt))
        {
            if (!DateTimeOffset.TryParse(expiresAt, out var parsed))
                throw new McpException($"Invalid expiresAt '{expiresAt}'. Use an ISO 8601 timestamp.");

            if (parsed <= clock.UtcNow)
                throw new McpException("expiresAt must be in the future.");

            expires = parsed;
        }

        if (maxDownloads is <= 0)
            throw new McpException("maxDownloads must be greater than zero.");

        string? passwordHashBase64 = null;
        byte[]? passwordSalt = null;

        if (!string.IsNullOrEmpty(password))
        {
            var (hash, salt) = await QuickSharePasswordHasher.Hash(
                password);

            passwordHashBase64 = hash;
            passwordSalt = salt;
        }

        var parameters = new CreateShareLinkParams
        {
            WorkspaceExternalId = workspaceExternalId,
            Name = trimmedName,
            FileExternalIds = files,
            FolderExternalIds = folders,
            ExcludedFileExternalIds = excludedFiles,
            ExcludedFolderExternalIds = excludedFolders,
            ExpiresAt = expires,
            MaxDownloads = maxDownloads,
            PasswordHashBase64 = passwordHashBase64,
            PasswordSalt = passwordSalt
        };

        var definition = AgentToolCatalog.TryGet(AgentToolNames.CreateShareLink)!;

        var workspaceOverride = workspaceToolOverrideReader.TryGet(
            agentId: agent.Id,
            workspaceId: workspace.Id,
            toolName: AgentToolNames.CreateShareLink);

        var effective = AgentToolCatalog.Resolve(
            agent,
            definition,
            workspaceOverride);

        if (!effective.IsUsable)
            throw new McpException("The create_share_link tool is not enabled for this workspace.");

        if (effective.RequiresApproval)
        {
            var expiresAtUtc = clock.UtcNow.AddHours(
                operationsOptions.ApprovalWindowHours);

            var operationId = await operationLedger.CreatePending(
                agentId: agent.Id,
                workspaceId: workspace.Id,
                toolName: AgentToolNames.CreateShareLink,
                paramsJson: Json.Serialize(parameters),
                expiresAt: expiresAtUtc,
                cancellationToken: cancellationToken);

            return AgentToolResponse.WaitsForApproval(
                approvalRequestId: operationId.Value,
                expiresAt: expiresAtUtc);
        }

        var result = await createShareLinkOperation.Execute(
            httpContext,
            parameters,
            cancellationToken);

        return AgentToolResponse.Executed(
            result);
    }
}
