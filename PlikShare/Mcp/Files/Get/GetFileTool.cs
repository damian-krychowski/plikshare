using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PlikShare.Agents.Middleware;
using PlikShare.AuditLog;
using PlikShare.Files.Id;
using PlikShare.Mcp.Files.Get.Contracts;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.Files.Get;

[McpServerToolType]
public class GetFileTool
{
    [McpServerTool(Name = "get_file")]
    [Description("Returns the details of a single file by its external id: name, extension, content type, " +
                 "size, creation time and the folder path it lives in. The file is resolved across all " +
                 "workspaces the agent can access; if the agent has no access to it, the tool reports it as " +
                 "not found without revealing whether it exists.")]
    public static async Task<GetFileResponseDto> Execute(
        IHttpContextAccessor httpContextAccessor,
        WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
        GetFileForAgentQuery getFileForAgentQuery,
        AuditLogService auditLogService,
        [Description("External id of the file.")]
        string fileExternalId,
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
