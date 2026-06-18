using PlikShare.Agents.Id;
using PlikShare.Boxes.Id;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;

namespace PlikShare.Agents.Tools;

public class AgentToolBoxOverrideQuery(DbWriteQueue dbWriteQueue)
{
    public Task<Result> Upsert(
        AgentExtId agentExternalId,
        BoxExtId boxExternalId,
        string toolName,
        bool? isEnabled,
        bool? requiresApproval,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => UpsertOperation(
                context,
                agentExternalId,
                boxExternalId,
                toolName,
                isEnabled,
                requiresApproval),
            cancellationToken: cancellationToken);
    }

    public Task<Result> Reset(
        AgentExtId agentExternalId,
        BoxExtId boxExternalId,
        string toolName,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ResetOperation(
                context,
                agentExternalId,
                boxExternalId,
                toolName),
            cancellationToken: cancellationToken);
    }

    private static Result UpsertOperation(
        SqliteWriteContext context,
        AgentExtId agentExternalId,
        BoxExtId boxExternalId,
        string toolName,
        bool? isEnabled,
        bool? requiresApproval)
    {
        var resolved = Resolve(context, agentExternalId, boxExternalId);

        if (resolved.Code != ResultCode.Ok)
            return resolved;

        context.Connection
            .NonQueryCmd(
                sql: """
                     INSERT INTO atbo_agent_tool_box_overrides (
                         atbo_agent_id,
                         atbo_box_id,
                         atbo_tool_name,
                         atbo_is_enabled,
                         atbo_requires_approval
                     ) VALUES (
                         $agentId,
                         $boxId,
                         $toolName,
                         $isEnabled,
                         $requiresApproval
                     )
                     ON CONFLICT (atbo_agent_id, atbo_box_id, atbo_tool_name)
                     DO UPDATE SET
                         atbo_is_enabled = $isEnabled,
                         atbo_requires_approval = $requiresApproval
                     """)
            .WithParameter("$agentId", resolved.AgentId)
            .WithParameter("$boxId", resolved.BoxId)
            .WithParameter("$toolName", toolName)
            .WithParameter("$isEnabled", isEnabled)
            .WithParameter("$requiresApproval", requiresApproval)
            .Execute();

        return resolved;
    }

    private static Result ResetOperation(
        SqliteWriteContext context,
        AgentExtId agentExternalId,
        BoxExtId boxExternalId,
        string toolName)
    {
        var resolved = Resolve(context, agentExternalId, boxExternalId);

        if (resolved.Code != ResultCode.Ok)
            return resolved;

        context.Connection
            .NonQueryCmd(
                sql: """
                     DELETE FROM atbo_agent_tool_box_overrides
                     WHERE atbo_agent_id = $agentId
                         AND atbo_box_id = $boxId
                         AND atbo_tool_name = $toolName
                     """)
            .WithParameter("$agentId", resolved.AgentId)
            .WithParameter("$boxId", resolved.BoxId)
            .WithParameter("$toolName", toolName)
            .Execute();

        return resolved;
    }

    private static Result Resolve(
        SqliteWriteContext context,
        AgentExtId agentExternalId,
        BoxExtId boxExternalId)
    {
        var agent = context.Connection
            .OneRowCmd(
                sql: "SELECT a_id, a_name FROM a_agents WHERE a_external_id = $externalId LIMIT 1",
                readRowFunc: reader => (Id: reader.GetInt32(0), Name: reader.GetString(1)))
            .WithParameter("$externalId", agentExternalId.Value)
            .Execute();

        if (agent.IsEmpty)
            return new Result(ResultCode.AgentNotFound);

        var box = context.Connection
            .OneRowCmd(
                sql: """
                     SELECT bo_id, bo_name
                     FROM bo_boxes
                     WHERE bo_external_id = $externalId
                         AND bo_is_being_deleted = FALSE
                     LIMIT 1
                     """,
                readRowFunc: reader => (Id: reader.GetInt32(0), Name: reader.GetString(1)))
            .WithParameter("$externalId", boxExternalId.Value)
            .Execute();

        if (box.IsEmpty)
            return new Result(ResultCode.BoxNotFound, AgentId: agent.Value.Id, AgentName: agent.Value.Name);

        return new Result(
            ResultCode.Ok,
            AgentId: agent.Value.Id,
            AgentName: agent.Value.Name,
            BoxId: box.Value.Id,
            BoxName: box.Value.Name);
    }

    public enum ResultCode
    {
        Ok = 0,
        AgentNotFound,
        BoxNotFound
    }

    public readonly record struct Result(
        ResultCode Code,
        int AgentId = 0,
        string? AgentName = null,
        int BoxId = 0,
        string? BoxName = null);
}
