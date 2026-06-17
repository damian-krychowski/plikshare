using PlikShare.Agents.Id;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;

namespace PlikShare.Agents.Tools;

public class AgentToolConfigQuery(DbWriteQueue dbWriteQueue)
{
    public Task<Result> Upsert(
        AgentExtId agentExternalId,
        string toolName,
        bool isEnabled,
        bool requiresApproval,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => UpsertOperation(
                context,
                agentExternalId,
                toolName,
                isEnabled,
                requiresApproval),
            cancellationToken: cancellationToken);
    }

    public Task<Result> Reset(
        AgentExtId agentExternalId,
        string toolName,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ResetOperation(
                context,
                agentExternalId,
                toolName),
            cancellationToken: cancellationToken);
    }

    private static Result UpsertOperation(
        SqliteWriteContext context,
        AgentExtId agentExternalId,
        string toolName,
        bool isEnabled,
        bool requiresApproval)
    {
        var agent = GetAgent(context, agentExternalId);

        if (agent is null)
            return new Result(ResultCode.NotFound);

        context.Connection
            .NonQueryCmd(
                sql: """
                     INSERT INTO atc_agent_tool_configs (
                         atc_agent_id,
                         atc_tool_name,
                         atc_is_enabled,
                         atc_requires_approval
                     ) VALUES (
                         $agentId,
                         $toolName,
                         $isEnabled,
                         $requiresApproval
                     )
                     ON CONFLICT (atc_agent_id, atc_tool_name)
                     DO UPDATE SET
                         atc_is_enabled = $isEnabled,
                         atc_requires_approval = $requiresApproval
                     """)
            .WithParameter("$agentId", agent.Value.Id)
            .WithParameter("$toolName", toolName)
            .WithParameter("$isEnabled", isEnabled)
            .WithParameter("$requiresApproval", requiresApproval)
            .Execute();

        return new Result(ResultCode.Ok, agent.Value.Name);
    }

    private static Result ResetOperation(
        SqliteWriteContext context,
        AgentExtId agentExternalId,
        string toolName)
    {
        var agent = GetAgent(context, agentExternalId);

        if (agent is null)
            return new Result(ResultCode.NotFound);

        context.Connection
            .NonQueryCmd(
                sql: """
                     DELETE FROM atc_agent_tool_configs
                     WHERE atc_agent_id = $agentId
                         AND atc_tool_name = $toolName
                     """)
            .WithParameter("$agentId", agent.Value.Id)
            .WithParameter("$toolName", toolName)
            .Execute();

        return new Result(ResultCode.Ok, agent.Value.Name);
    }

    private static (int Id, string Name)? GetAgent(
        SqliteWriteContext context,
        AgentExtId agentExternalId)
    {
        var result = context.Connection
            .OneRowCmd(
                sql: "SELECT a_id, a_name FROM a_agents WHERE a_external_id = $externalId LIMIT 1",
                readRowFunc: reader => (Id: reader.GetInt32(0), Name: reader.GetString(1)))
            .WithParameter("$externalId", agentExternalId.Value)
            .Execute();

        return result.IsEmpty ? null : result.Value;
    }

    public enum ResultCode
    {
        Ok = 0,
        NotFound
    }

    public readonly record struct Result(
        ResultCode Code,
        string? AgentName = null);
}
