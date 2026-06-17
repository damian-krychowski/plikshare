using PlikShare.Agents.Id;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Workspaces.Id;

namespace PlikShare.Agents.Tools;

public class AgentToolWorkspaceOverrideQuery(DbWriteQueue dbWriteQueue)
{
    public Task<Result> Upsert(
        AgentExtId agentExternalId,
        WorkspaceExtId workspaceExternalId,
        string toolName,
        bool? isEnabled,
        bool? requiresApproval,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => UpsertOperation(
                context,
                agentExternalId,
                workspaceExternalId,
                toolName,
                isEnabled,
                requiresApproval),
            cancellationToken: cancellationToken);
    }

    public Task<Result> Reset(
        AgentExtId agentExternalId,
        WorkspaceExtId workspaceExternalId,
        string toolName,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ResetOperation(
                context,
                agentExternalId,
                workspaceExternalId,
                toolName),
            cancellationToken: cancellationToken);
    }

    private static Result UpsertOperation(
        SqliteWriteContext context,
        AgentExtId agentExternalId,
        WorkspaceExtId workspaceExternalId,
        string toolName,
        bool? isEnabled,
        bool? requiresApproval)
    {
        var resolved = Resolve(context, agentExternalId, workspaceExternalId);

        if (resolved.Code != ResultCode.Ok)
            return resolved;

        context.Connection
            .NonQueryCmd(
                sql: """
                     INSERT INTO atwo_agent_tool_workspace_overrides (
                         atwo_agent_id,
                         atwo_workspace_id,
                         atwo_tool_name,
                         atwo_is_enabled,
                         atwo_requires_approval
                     ) VALUES (
                         $agentId,
                         $workspaceId,
                         $toolName,
                         $isEnabled,
                         $requiresApproval
                     )
                     ON CONFLICT (atwo_agent_id, atwo_workspace_id, atwo_tool_name)
                     DO UPDATE SET
                         atwo_is_enabled = $isEnabled,
                         atwo_requires_approval = $requiresApproval
                     """)
            .WithParameter("$agentId", resolved.AgentId)
            .WithParameter("$workspaceId", resolved.WorkspaceId)
            .WithParameter("$toolName", toolName)
            .WithParameter("$isEnabled", isEnabled)
            .WithParameter("$requiresApproval", requiresApproval)
            .Execute();

        return resolved;
    }

    private static Result ResetOperation(
        SqliteWriteContext context,
        AgentExtId agentExternalId,
        WorkspaceExtId workspaceExternalId,
        string toolName)
    {
        var resolved = Resolve(context, agentExternalId, workspaceExternalId);

        if (resolved.Code != ResultCode.Ok)
            return resolved;

        context.Connection
            .NonQueryCmd(
                sql: """
                     DELETE FROM atwo_agent_tool_workspace_overrides
                     WHERE atwo_agent_id = $agentId
                         AND atwo_workspace_id = $workspaceId
                         AND atwo_tool_name = $toolName
                     """)
            .WithParameter("$agentId", resolved.AgentId)
            .WithParameter("$workspaceId", resolved.WorkspaceId)
            .WithParameter("$toolName", toolName)
            .Execute();

        return resolved;
    }

    private static Result Resolve(
        SqliteWriteContext context,
        AgentExtId agentExternalId,
        WorkspaceExtId workspaceExternalId)
    {
        var agent = context.Connection
            .OneRowCmd(
                sql: "SELECT a_id, a_name FROM a_agents WHERE a_external_id = $externalId LIMIT 1",
                readRowFunc: reader => (Id: reader.GetInt32(0), Name: reader.GetString(1)))
            .WithParameter("$externalId", agentExternalId.Value)
            .Execute();

        if (agent.IsEmpty)
            return new Result(ResultCode.AgentNotFound);

        var workspace = context.Connection
            .OneRowCmd(
                sql: """
                     SELECT w_id, w_name
                     FROM w_workspaces
                     WHERE w_external_id = $externalId
                         AND w_is_being_deleted = FALSE
                     LIMIT 1
                     """,
                readRowFunc: reader => (Id: reader.GetInt32(0), Name: reader.GetString(1)))
            .WithParameter("$externalId", workspaceExternalId.Value)
            .Execute();

        if (workspace.IsEmpty)
            return new Result(ResultCode.WorkspaceNotFound, AgentId: agent.Value.Id, AgentName: agent.Value.Name);

        return new Result(
            ResultCode.Ok,
            AgentId: agent.Value.Id,
            AgentName: agent.Value.Name,
            WorkspaceId: workspace.Value.Id,
            WorkspaceName: workspace.Value.Name);
    }

    public enum ResultCode
    {
        Ok = 0,
        AgentNotFound,
        WorkspaceNotFound
    }

    public readonly record struct Result(
        ResultCode Code,
        int AgentId = 0,
        string? AgentName = null,
        int WorkspaceId = 0,
        string? WorkspaceName = null);
}
