using Microsoft.AspNetCore.Http;
using PlikShare.AuditLog;
using PlikShare.Boxes.Cache;
using PlikShare.Boxes.Get;
using PlikShare.Boxes.Id;
using PlikShare.Mcp.Boxes.Get.Contracts;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.Boxes.Get;

/// <summary>
/// The reusable core of get_box: re-validates the agent's workspace access, resolves the box within that
/// workspace and reads its details and content, writing the audit entry. Called directly by the tool when
/// no approval is required, and by the execute flow once a human has approved the operation. The read is
/// idempotent, so the execute flow simply re-reads.
/// </summary>
public class GetBoxAgentOperation(
    WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
    BoxCache boxCache,
    GetBoxQuery getBoxQuery,
    AuditLogService auditLogService)
{
    public async Task<GetBoxResponseDto> Execute(
        HttpContext httpContext,
        GetBoxParams parameters,
        CancellationToken cancellationToken)
    {
        var membership = await httpContext.GetWorkspaceAgentMembershipDetails(
            workspaceAgentMembershipCache,
            WorkspaceExtId.Parse(parameters.WorkspaceExternalId),
            cancellationToken);

        var workspace = membership.Workspace;
        workspace.ThrowIfFullyEncrypted();

        var box = await httpContext.GetAgentBoxInWorkspace(
            boxCache,
            workspace,
            BoxExtId.Parse(parameters.BoxExternalId),
            cancellationToken);

        var details = getBoxQuery.Execute(
            box: box,
            workspaceEncryptionSession: null);

        await auditLogService.Log(
            Audit.Agent.BoxViewedEntry(
                actor: httpContext.GetAuditLogActorContext(),
                workspaceExternalId: parameters.WorkspaceExternalId,
                boxExternalId: parameters.BoxExternalId),
            cancellationToken);

        return new GetBoxResponseDto
        {
            ExternalId = details.Details.ExternalId,
            Name = details.Details.Name,
            IsEnabled = details.Details.IsEnabled,
            FolderPath = details.Details.FolderPath
                .Select(folder => new GetBoxResponseDto.FolderPathItemDto
                {
                    ExternalId = folder.ExternalId,
                    Name = folder.Name
                })
                .ToList(),
            MembersCount = details.Members.Count,
            LinksCount = details.Links.Count,
            Subfolders = details.Subfolders
                ?.Select(subfolder => new GetBoxResponseDto.SubfolderDto
                {
                    ExternalId = subfolder.ExternalId,
                    Name = subfolder.Name
                })
                .ToList(),
            Files = details.Files
                ?.Select(file => new GetBoxResponseDto.FileDto
                {
                    ExternalId = file.ExternalId,
                    Name = file.Name,
                    Extension = file.Extension,
                    SizeInBytes = file.SizeInBytes
                })
                .ToList()
        };
    }
}
