using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
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

/// <summary>
/// The reusable core of get_file_download_link: resolves the file across the agent's workspaces,
/// re-validates access and creates a short-lived pre-signed download link, writing the audit entry.
/// Called directly by the tool when no approval is required, and by the execute flow once a human has
/// approved the operation. The link's lifetime starts when it is created, so the expiry is computed
/// here (at execute time) from the stored duration.
/// </summary>
public class GetFileDownloadLinkAgentOperation(
    WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
    GetFileForAgentQuery getFileForAgentQuery,
    GetFileDownloadLinkOperation getFileDownloadLinkOperation,
    AuditLogService auditLogService,
    IClock clock)
{
    private const int DefaultExpiryMinutes = 15;
    private const int MinExpiryMinutes = 1;
    private const int MaxExpiryMinutes = 24 * 60;

    public async Task<GetFileDownloadLinkResponseDto> Execute(
        HttpContext httpContext,
        GetFileDownloadLinkParams parameters,
        CancellationToken cancellationToken)
    {
        var agent = await httpContext.GetAgentContext();

        var file = getFileForAgentQuery.Execute(
            FileExtId.Parse(parameters.FileExternalId));

        if (file is null)
            throw new McpException($"File '{parameters.FileExternalId}' was not found.");

        var membership = await workspaceAgentMembershipCache.TryGetWorkspaceAgentMembership(
            workspaceExternalId: WorkspaceExtId.Parse(file.WorkspaceExternalId),
            agentExternalId: agent.ExternalId,
            cancellationToken: cancellationToken);

        if (membership is null || !membership.IsAvailableForAgent)
            throw new McpException($"File '{parameters.FileExternalId}' was not found.");

        var workspace = membership.Workspace;
        workspace.ThrowIfFullyEncrypted();

        var effectiveMinutes = Math.Clamp(
            parameters.ExpiresInMinutes ?? DefaultExpiryMinutes,
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
                throw new McpException($"File '{parameters.FileExternalId}' was not found.");

            default:
                throw new McpException(
                    $"Could not create a download link for file '{parameters.FileExternalId}': {result.Code}.");
        }
    }
}
