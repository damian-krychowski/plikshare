using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using PlikShare.Agents.Middleware;
using PlikShare.AuditLog;
using PlikShare.Files.Id;
using PlikShare.Mcp.Files.Get.Contracts;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.Files.Get;

/// <summary>
/// The reusable core of get_file: resolves the file across the agent's workspaces, re-validates
/// access and returns its details, writing the audit entry. Called directly by the tool when no
/// approval is required, and by the execute flow once a human has approved the operation. The read is
/// idempotent, so the execute flow simply re-reads.
/// </summary>
public class GetFileAgentOperation(
    WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
    GetFileForAgentQuery getFileForAgentQuery,
    AuditLogService auditLogService)
{
    public async Task<GetFileResponseDto> Execute(
        HttpContext httpContext,
        GetFileParams parameters,
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

        membership.Workspace.ThrowIfFullyEncrypted();

        await auditLogService.Log(
            Audit.Agent.FileViewedEntry(
                actor: httpContext.GetAuditLogActorContext(),
                workspaceExternalId: file.WorkspaceExternalId,
                fileExternalId: file.ExternalId),
            cancellationToken);

        return new GetFileResponseDto
        {
            ExternalId = file.ExternalId,
            Name = file.Name,
            Extension = file.Extension,
            ContentType = file.ContentType,
            SizeInBytes = file.SizeInBytes,
            CreatedAt = file.CreatedAt,
            Path = file.Path
                .Select(item => new FilePathItemDto
                {
                    ExternalId = item.ExternalId,
                    Name = item.Name
                })
                .ToList()
        };
    }
}
