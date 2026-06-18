using PlikShare.Agents.Cache;
using PlikShare.Agents.Tools;
using PlikShare.Files.Id;
using PlikShare.Mcp.Files.Get;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;

namespace PlikShare.Mcp.Files;

/// <summary>
/// Tools that target a file by its external id (get_file, read_file, get_file_download_link) don't take
/// a workspace argument — the workspace is the file's own. This locator resolves that workspace
/// (best-effort) so the invocation-time gate can apply a per-workspace override before the operation
/// runs. When the file can't be resolved or the agent has no access it returns <see cref="NotLocated"/>
/// and the gate falls back to the agent's global config; the operation stays authoritative and reports
/// the file as not found.
/// </summary>
public class AgentFileWorkspaceLocator(
    GetFileForAgentQuery getFileForAgentQuery,
    WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
    AgentWorkspaceToolOverrideReader workspaceToolOverrideReader)
{
    public async Task<Located> Locate(
        AgentContext agent,
        string fileExternalId,
        string toolName,
        CancellationToken cancellationToken)
    {
        if (!FileExtId.TryParse(fileExternalId, null, out var fileId))
            return Located.NotLocated;

        var file = getFileForAgentQuery.Execute(fileId);

        if (file is null)
            return Located.NotLocated;

        var membership = await workspaceAgentMembershipCache.TryGetWorkspaceAgentMembership(
            workspaceExternalId: WorkspaceExtId.Parse(file.WorkspaceExternalId),
            agentExternalId: agent.ExternalId,
            cancellationToken: cancellationToken);

        if (membership is null || !membership.IsAvailableForAgent)
            return Located.NotLocated;

        var workspaceId = membership.Workspace.Id;

        var workspaceOverride = workspaceToolOverrideReader.TryGet(
            agentId: agent.Id,
            workspaceId: workspaceId,
            toolName: toolName);

        return new Located(workspaceId, workspaceOverride);
    }

    public readonly record struct Located(
        int? WorkspaceId,
        AgentToolScopeOverride? Override)
    {
        public static Located NotLocated => new(null, null);
    }
}
