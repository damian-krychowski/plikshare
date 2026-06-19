using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using PlikShare.AuditLog;
using PlikShare.Boxes.Cache;
using PlikShare.Boxes.Delete;
using PlikShare.Boxes.Id;
using PlikShare.Core.CorrelationId;
using PlikShare.Mcp.Boxes.Delete.Contracts;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.Boxes.Delete;

/// <summary>
/// The reusable core of delete_box: re-validates the agent's workspace access, resolves the box within
/// that workspace and schedules it for deletion (soft delete), invalidating the box cache and writing the
/// audit entry. Called directly by the tool when no approval is required, and by the execute flow once a
/// human has approved the operation.
/// </summary>
public class DeleteBoxAgentOperation(
    WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
    BoxCache boxCache,
    ScheduleBoxesDeleteQuery scheduleBoxesDeleteQuery,
    AuditLogService auditLogService)
{
    public async Task<DeleteBoxResponseDto> Execute(
        HttpContext httpContext,
        DeleteBoxParams parameters,
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

        var code = await scheduleBoxesDeleteQuery.Execute(
            box: box,
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);

        switch (code)
        {
            case ScheduleBoxesDeleteQuery.ResultCode.Ok:
                await boxCache.InvalidateEntry(box.Id, cancellationToken);

                await auditLogService.Log(
                    Audit.Box.DeletedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: workspace.ToAuditLogWorkspaceRef(),
                        box: new Audit.BoxRef { ExternalId = box.ExternalId, Name = box.Name }),
                    cancellationToken);

                return new DeleteBoxResponseDto
                {
                    BoxExternalId = parameters.BoxExternalId
                };

            case ScheduleBoxesDeleteQuery.ResultCode.BoxesNotFound:
                throw new McpException(
                    $"Box '{parameters.BoxExternalId}' was not found in workspace '{parameters.WorkspaceExternalId}'.");

            default:
                throw new McpException($"Could not delete the box: {code}.");
        }
    }
}
