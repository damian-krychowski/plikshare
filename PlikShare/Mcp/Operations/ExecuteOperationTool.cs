using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PlikShare.Agents.Middleware;
using PlikShare.Agents.Operations;
using PlikShare.Agents.Operations.Id;
using PlikShare.Agents.Tools;
using PlikShare.Core.Clock;
using PlikShare.Core.Utils;

namespace PlikShare.Mcp.Operations;

[McpServerToolType]
public class ExecuteOperationTool
{
    [McpServerTool(Name = AgentToolNames.ExecuteOperation)]
    [Description("Runs an operation that was waiting for approval, once a human has approved it. Pass the " +
                 "approvalRequestId returned by the original tool call. Returns status 'executed' with the " +
                 "tool's result; or 'waits_for_approval' if not yet approved; or 'rejected' if it was denied, " +
                 "expired or failed. Safe to call again — an already-executed operation returns its result " +
                 "without running twice.")]
    public static async Task<AgentToolResponse> Execute(
        IHttpContextAccessor httpContextAccessor,
        AgentOperationLedger operationLedger,
        AgentOperationDispatcher dispatcher,
        IClock clock,
        [Description("The approvalRequestId returned when the operation was submitted.")]
        string approvalRequestId,
        CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext!;
        var agent = await httpContext.GetAgentContext();

        var operation = operationLedger.GetByExternalId(
            AgentOperationExtId.Parse(approvalRequestId));

        if (operation is null || operation.AgentId != agent.Id)
            throw new McpException($"Operation '{approvalRequestId}' was not found.");

        var status = operation.Status;

        if (status == AgentOperationStatuses.Pending && clock.UtcNow > operation.ExpiresAt)
            status = AgentOperationStatuses.Expired;

        switch (status)
        {
            case AgentOperationStatuses.Pending:
                return AgentToolResponse.WaitsForApproval(
                    operation.ExternalId.Value,
                    operation.ExpiresAt);

            case AgentOperationStatuses.Denied:
                return AgentToolResponse.Rejected("denied", "The operation was denied by a human.");

            case AgentOperationStatuses.Expired:
                return AgentToolResponse.Rejected("expired", "The operation expired before it was approved.");

            case AgentOperationStatuses.Executed:
                return AgentToolResponse.Executed(ParseStoredResult(operation.ResultJson));

            case AgentOperationStatuses.Failed:
                return AgentToolResponse.Rejected("failed", operation.ResultJson ?? "The operation failed.");

            case AgentOperationStatuses.Approved:
            {
                var plan = dispatcher.Plan(httpContext, operation);

                try
                {
                    var result = await plan.Execute(
                        cancellationToken);

                    var resultJson = Json.Serialize(
                        result,
                        result.GetType());

                    if (plan.PersistsResult)
                        await operationLedger.Complete(
                            operation.ExternalId,
                            resultJson,
                            cancellationToken);

                    return AgentToolResponse.Executed(ParseStoredResult(resultJson));
                }
                catch (Exception exception)
                {
                    if (plan.PersistsResult)
                        await operationLedger.Fail(
                            operation.ExternalId,
                            exception.Message,
                            cancellationToken);

                    return AgentToolResponse.Rejected("failed", exception.Message);
                }
            }

            default:
                throw new McpException($"Operation is in an unexpected state '{status}'.");
        }
    }

    private static object? ParseStoredResult(string? resultJson) =>
        resultJson is null ? null : JsonDocument.Parse(resultJson).RootElement.Clone();
}
