using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PlikShare.Agents.Middleware;
using PlikShare.AuditLog;
using PlikShare.Core.Clock;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Files.Download;
using PlikShare.Files.Id;
using PlikShare.Mcp.Files.DownloadLink.Contracts;
using PlikShare.Mcp.Files.Get;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.Files.DownloadLink;

[McpServerToolType]
public class GetFileDownloadLinkTool
{
    private const int DefaultExpiryMinutes = 15;
    private const int MinExpiryMinutes = 1;
    private const int MaxExpiryMinutes = 24 * 60;

    [McpServerTool(Name = "get_file_download_link")]
    [Description("Creates a short-lived, ready-to-download link for a file the agent can access, to hand to a " +
                 "user. Anyone with the link can download the file without logging in until it expires, so " +
                 "treat it as a capability and keep the expiry short. The file is resolved across all " +
                 "workspaces the agent can access; if the agent has no access it is reported as not found.")]
    public static async Task<GetFileDownloadLinkResponseDto> Execute(
        IHttpContextAccessor httpContextAccessor,
        WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
        GetFileForAgentQuery getFileForAgentQuery,
        GetFileDownloadLinkOperation getFileDownloadLinkOperation,
        AuditLogService auditLogService,
        IClock clock,
        [Description("External id of the file to create a download link for.")]
        string fileExternalId,
        [Description("How long the link stays valid, in minutes. Default 15, minimum 1, maximum 1440 (24h).")]
        int? expiresInMinutes = null,
        CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext!;

        var agent = await httpContext.GetAgentContext();

        var file = getFileForAgentQuery.Execute(
            FileExtId.Parse(fileExternalId));

        if (file is null)
            throw new McpException($"File '{fileExternalId}' was not found.");

        var membership = await workspaceAgentMembershipCache.TryGetWorkspaceAgentMembership(
            workspaceExternalId: WorkspaceExtId.Parse(file.WorkspaceExternalId),
            agentExternalId: agent.ExternalId,
            cancellationToken: cancellationToken);

        if (membership is null || !membership.IsAvailableForAgent)
            throw new McpException($"File '{fileExternalId}' was not found.");

        var workspace = membership.Workspace;
        workspace.ThrowIfFullyEncrypted();

        var effectiveMinutes = Math.Clamp(
            expiresInMinutes ?? DefaultExpiryMinutes,
            MinExpiryMinutes,
            MaxExpiryMinutes);

        var expiresAt = clock.UtcNow.AddMinutes(effectiveMinutes);

        var result = await getFileDownloadLinkOperation.Execute(
            workspace: workspace,
            fileExternalId: FileExtId.Parse(file.ExternalId),
            contentDisposition: ContentDispositionType.Attachment,
            boxFolderId: null,
            boxLinkId: null,
            userIdentity: new AgentIdentity(membership.Agent.ExternalId),
            enforceInternalPassThrough: false,
            workspaceEncryptionSession: null,
            expiresAt: expiresAt,
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case GetFileDownloadLinkOperation.ResultCode.Ok:
                await auditLogService.Log(
                    Audit.Agent.FileDownloadLinkGeneratedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspaceExternalId: file.WorkspaceExternalId,
                        fileExternalId: file.ExternalId,
                        expiresAt: expiresAt),
                    cancellationToken);

                return new GetFileDownloadLinkResponseDto
                {
                    Url = result.DownloadPreSignedUrl!,
                    FileName = file.Name + file.Extension,
                    ExpiresAt = expiresAt
                };

            case GetFileDownloadLinkOperation.ResultCode.FileNotFound:
                throw new McpException($"File '{fileExternalId}' was not found.");

            default:
                throw new McpException(
                    $"Could not create a download link for file '{fileExternalId}': {result.Code}.");
        }
    }
}
