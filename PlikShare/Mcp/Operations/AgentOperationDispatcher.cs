using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using PlikShare.Agents.Operations;
using PlikShare.Agents.Tools;
using PlikShare.Core.Utils;
using PlikShare.Mcp.BulkDelete;

namespace PlikShare.Mcp.Operations;

/// <summary>
/// Resolves an approved operation into a plan: how to run it and whether its result must be
/// persisted. Each approval-capable tool owns one branch and declares its own
/// <see cref="AgentOperationPlan.PersistsResult"/> — mutating tools persist (exactly-once,
/// the commit never re-runs them), idempotent read tools do not (the commit simply re-reads).
/// </summary>
public class AgentOperationDispatcher(
    BulkDeleteForAgentExecutor bulkDeleteExecutor)
{
    public AgentOperationPlan Plan(
        HttpContext httpContext,
        AgentOperation operation)
    {
        switch (operation.ToolName)
        {
            case AgentToolNames.BulkDelete:
                return new AgentOperationPlan(
                    PersistsResult: true,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<BulkDeleteParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await bulkDeleteExecutor.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            default:
                throw new McpException($"Operation '{operation.ToolName}' cannot be committed.");
        }
    }
}

public sealed record AgentOperationPlan(
    bool PersistsResult,
    Func<CancellationToken, Task<object>> Execute);
