using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using PlikShare.AuditLog;
using PlikShare.Mcp.ShareLinks.Create.Contracts;
using PlikShare.QuickShares;
using PlikShare.QuickShares.Create;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.ShareLinks.Create;

/// <summary>
/// The reusable core of create_share_link: re-validates the agent's workspace access and creates the
/// public quick share, writing the audit entry. Called directly by the tool when no approval is
/// required, and by the execute flow once a human has approved the operation. The password is already
/// hashed at submit time, so the plain password is never stored in the operation parameters.
/// </summary>
public class CreateShareLinkAgentOperation(
    WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
    CreateQuickShareQuery createQuickShareQuery,
    QuickShareUrlBuilder urlBuilder,
    AuditLogService auditLogService)
{
    public async Task<CreateShareLinkResponseDto> Execute(
        HttpContext httpContext,
        CreateShareLinkParams parameters,
        CancellationToken cancellationToken)
    {
        var membership = await httpContext.GetWorkspaceAgentMembershipDetails(
            workspaceAgentMembershipCache,
            WorkspaceExtId.Parse(parameters.WorkspaceExternalId),
            cancellationToken);

        var workspace = membership.Workspace;
        workspace.ThrowIfFullyEncrypted();

        var agent = membership.Agent;

        var files = parameters.FileExternalIds.ToList();
        var folders = parameters.FolderExternalIds.ToList();
        var excludedFiles = parameters.ExcludedFileExternalIds.ToList();
        var excludedFolders = parameters.ExcludedFolderExternalIds.ToList();

        var result = await createQuickShareQuery.Execute(
            workspace: workspace,
            creatorExternalId: agent.Owner.ExternalId,
            creatorAgentExternalId: agent.ExternalId,
            name: parameters.Name,
            customSlug: null,
            selectedFiles: files,
            selectedFolders: folders,
            excludedFiles: excludedFiles,
            excludedFolders: excludedFolders,
            mode: QuickShareMode.Browser,
            allowIndividualFileDownload: true,
            expiresAt: parameters.ExpiresAt,
            passwordHashBase64: parameters.PasswordHashBase64,
            passwordSalt: parameters.PasswordSalt,
            maxDownloads: parameters.MaxDownloads,
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case CreateQuickShareQuery.ResultCode.Ok:
                var selectedCtx = auditLogService.GetBulkItemsContext(
                    folderExternalIds: folders,
                    fileExternalIds: files,
                    fileUploadExternalIds: []);

                var excludedCtx = auditLogService.GetBulkItemsContext(
                    folderExternalIds: excludedFolders,
                    fileExternalIds: excludedFiles,
                    fileUploadExternalIds: []);

                await auditLogService.Log(
                    Audit.QuickShare.CreatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: workspace.ToAuditLogWorkspaceRef(),
                        quickShare: new Audit.QuickShareRef
                        {
                            ExternalId = result.QuickShareExternalId,
                            Name = parameters.Name
                        },
                        mode: QuickShareMode.Browser,
                        allowIndividualFileDownload: true,
                        hasPassword: parameters.PasswordHashBase64 is not null,
                        maxDownloads: parameters.MaxDownloads,
                        expiresAt: parameters.ExpiresAt,
                        selectedFiles: selectedCtx.Files,
                        selectedFolders: selectedCtx.Folders,
                        excludedFiles: excludedCtx.Files,
                        excludedFolders: excludedCtx.Folders),
                    cancellationToken);

                return new CreateShareLinkResponseDto
                {
                    ExternalId = result.QuickShareExternalId.Value,
                    Url = urlBuilder.BuildUrl(result.Slug!)
                };

            case CreateQuickShareQuery.ResultCode.ItemsNotFound:
                throw new McpException(
                    "One or more of the specified files or folders were not found in the workspace.");

            case CreateQuickShareQuery.ResultCode.CreatorNotFound:
                throw new McpException("The share link creator could not be resolved.");

            default:
                throw new McpException($"Could not create the share link: {result.Code}.");
        }
    }
}
