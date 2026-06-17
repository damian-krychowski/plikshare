using System.ComponentModel;
using ModelContextProtocol.Server;
using PlikShare.Agents.Middleware;
using PlikShare.Agents.Operations;
using PlikShare.Agents.Tools;
using PlikShare.Core.Clock;

namespace PlikShare.Mcp.Operations;

[McpServerToolType]
public class CheckApprovalsTool
{
    [McpServerTool(Name = AgentToolNames.CheckApprovals)]
    [Description("Lists your operations that are waiting on a human decision and their statuses: " +
                 "'pending' (awaiting approval), 'approved' (ready — call execute_operation to run it), " +
                 "'denied' or 'expired'. Poll this after submitting an approval-gated tool; once an " +
                 "operation shows 'approved', call execute_operation with its approvalRequestId. " +
                 "Already-executed operations are not listed.")]
    public static async Task<CheckApprovalsResponseDto> Execute(
        IHttpContextAccessor httpContextAccessor,
        AgentOperationLedger operationLedger,
        IClock clock,
        CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext!;
        var agent = await httpContext.GetAgentContext();

        var now = clock.UtcNow;

        var approvals = operationLedger
            .ListOutstandingByAgent(agent.Id)
            .Select(operation => new CheckApprovalsResponseDto.Approval
            {
                ApprovalRequestId = operation.ExternalId.Value,
                ToolName = operation.ToolName,
                Status = operation.Status == AgentOperationStatuses.Pending && now > operation.ExpiresAt
                    ? AgentOperationStatuses.Expired
                    : operation.Status,
                CreatedAt = operation.CreatedAt.ToString("O"),
                ExpiresAt = operation.ExpiresAt.ToString("O")
            })
            .ToList();

        return new CheckApprovalsResponseDto
        {
            Approvals = approvals
        };
    }
}

public class CheckApprovalsResponseDto
{
    public required List<Approval> Approvals { get; init; }

    public class Approval
    {
        public required string ApprovalRequestId { get; init; }
        public required string ToolName { get; init; }
        public required string Status { get; init; }
        public required string CreatedAt { get; init; }
        public required string ExpiresAt { get; init; }
    }
}
