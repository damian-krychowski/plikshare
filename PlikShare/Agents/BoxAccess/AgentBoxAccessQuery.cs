using Microsoft.Data.Sqlite;
using PlikShare.Agents.Id;
using PlikShare.Boxes.Id;
using PlikShare.Boxes.Permissions;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.Agents.BoxAccess;

public class AgentBoxAccessQuery(
    DbWriteQueue dbWriteQueue,
    IClock clock)
{
    public Task<Result> Grant(
        AgentExtId agentExternalId,
        BoxExtId boxExternalId,
        BoxPermissions permissions,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => GrantOperation(
                dbWriteContext: context,
                agentExternalId: agentExternalId,
                boxExternalId: boxExternalId,
                permissions: permissions),
            cancellationToken: cancellationToken);
    }

    public Task<Result> Revoke(
        AgentExtId agentExternalId,
        BoxExtId boxExternalId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => RevokeOperation(
                dbWriteContext: context,
                agentExternalId: agentExternalId,
                boxExternalId: boxExternalId),
            cancellationToken: cancellationToken);
    }

    private Result GrantOperation(
        SqliteWriteContext dbWriteContext,
        AgentExtId agentExternalId,
        BoxExtId boxExternalId,
        BoxPermissions permissions)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var (code, targets) = ResolveTargets(
                dbWriteContext,
                transaction,
                agentExternalId,
                boxExternalId);

            if (code != ResultCode.Ok)
            {
                transaction.Rollback();
                return new Result(Code: code);
            }

            dbWriteContext.Connection
                .NonQueryCmd(
                    sql: """
                         INSERT INTO ba_box_agents (
                             ba_box_id,
                             ba_agent_id,
                             ba_allow_download,
                             ba_allow_upload,
                             ba_allow_list,
                             ba_allow_delete_file,
                             ba_allow_rename_file,
                             ba_allow_move_items,
                             ba_allow_create_folder,
                             ba_allow_delete_folder,
                             ba_allow_rename_folder,
                             ba_created_at
                         ) VALUES (
                             $boxId,
                             $agentId,
                             $allowDownload,
                             $allowUpload,
                             $allowList,
                             $allowDeleteFile,
                             $allowRenameFile,
                             $allowMoveItems,
                             $allowCreateFolder,
                             $allowDeleteFolder,
                             $allowRenameFolder,
                             $now
                         )
                         ON CONFLICT (ba_box_id, ba_agent_id) DO UPDATE SET
                             ba_allow_download = excluded.ba_allow_download,
                             ba_allow_upload = excluded.ba_allow_upload,
                             ba_allow_list = excluded.ba_allow_list,
                             ba_allow_delete_file = excluded.ba_allow_delete_file,
                             ba_allow_rename_file = excluded.ba_allow_rename_file,
                             ba_allow_move_items = excluded.ba_allow_move_items,
                             ba_allow_create_folder = excluded.ba_allow_create_folder,
                             ba_allow_delete_folder = excluded.ba_allow_delete_folder,
                             ba_allow_rename_folder = excluded.ba_allow_rename_folder
                         """,
                    transaction: transaction)
                .WithParameter("$boxId", targets.BoxId)
                .WithParameter("$agentId", targets.AgentId)
                .WithParameter("$allowDownload", permissions.AllowDownload)
                .WithParameter("$allowUpload", permissions.AllowUpload)
                .WithParameter("$allowList", permissions.AllowList)
                .WithParameter("$allowDeleteFile", permissions.AllowDeleteFile)
                .WithParameter("$allowRenameFile", permissions.AllowRenameFile)
                .WithParameter("$allowMoveItems", permissions.AllowMoveItems)
                .WithParameter("$allowCreateFolder", permissions.AllowCreateFolder)
                .WithParameter("$allowDeleteFolder", permissions.AllowDeleteFolder)
                .WithParameter("$allowRenameFolder", permissions.AllowRenameFolder)
                .WithParameter("$now", clock.UtcNow)
                .Execute();

            transaction.Commit();

            Log.Information("Agent '{AgentExternalId}' was granted access to Box '{BoxExternalId}'.",
                agentExternalId,
                boxExternalId);

            return new Result(
                Code: ResultCode.Ok,
                AgentName: targets.AgentName,
                BoxName: targets.BoxName);
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e, "Something went wrong while granting Agent '{AgentExternalId}' access to Box '{BoxExternalId}'.",
                agentExternalId,
                boxExternalId);

            throw;
        }
    }

    private Result RevokeOperation(
        SqliteWriteContext dbWriteContext,
        AgentExtId agentExternalId,
        BoxExtId boxExternalId)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var (code, targets) = ResolveTargets(
                dbWriteContext,
                transaction,
                agentExternalId,
                boxExternalId);

            if (code != ResultCode.Ok)
            {
                transaction.Rollback();
                return new Result(Code: code);
            }

            dbWriteContext.Connection
                .NonQueryCmd(
                    sql: """
                         DELETE FROM ba_box_agents
                         WHERE ba_box_id = $boxId
                             AND ba_agent_id = $agentId
                         """,
                    transaction: transaction)
                .WithParameter("$boxId", targets.BoxId)
                .WithParameter("$agentId", targets.AgentId)
                .Execute();

            transaction.Commit();

            Log.Information("Agent '{AgentExternalId}' access to Box '{BoxExternalId}' was revoked.",
                agentExternalId,
                boxExternalId);

            return new Result(
                Code: ResultCode.Ok,
                AgentName: targets.AgentName,
                BoxName: targets.BoxName);
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e, "Something went wrong while revoking Agent '{AgentExternalId}' access to Box '{BoxExternalId}'.",
                agentExternalId,
                boxExternalId);

            throw;
        }
    }

    private static (ResultCode Code, Targets Targets) ResolveTargets(
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction,
        AgentExtId agentExternalId,
        BoxExtId boxExternalId)
    {
        var agent = dbWriteContext
            .OneRowCmd(
                sql: """
                     SELECT
                         a_id,
                         a_name
                     FROM a_agents
                     WHERE a_external_id = $externalId
                     LIMIT 1
                     """,
                readRowFunc: reader => new AgentRow(
                    Id: reader.GetInt32(0),
                    Name: reader.GetString(1)),
                transaction: transaction)
            .WithParameter("$externalId", agentExternalId.Value)
            .Execute();

        if (agent.IsEmpty)
            return (ResultCode.AgentNotFound, default);

        var box = dbWriteContext
            .OneRowCmd(
                sql: """
                     SELECT
                         bo_id,
                         bo_name
                     FROM bo_boxes
                     WHERE bo_external_id = $externalId
                         AND bo_is_being_deleted = FALSE
                     LIMIT 1
                     """,
                readRowFunc: reader => new BoxRow(
                    Id: reader.GetInt32(0),
                    Name: reader.GetString(1)),
                transaction: transaction)
            .WithParameter("$externalId", boxExternalId.Value)
            .Execute();

        if (box.IsEmpty)
            return (ResultCode.BoxNotFound, default);

        return (ResultCode.Ok, new Targets(
            AgentId: agent.Value.Id,
            AgentName: agent.Value.Name,
            BoxId: box.Value.Id,
            BoxName: box.Value.Name));
    }

    private readonly record struct AgentRow(
        int Id,
        string Name);

    private readonly record struct BoxRow(
        int Id,
        string Name);

    private readonly record struct Targets(
        int AgentId,
        string AgentName,
        int BoxId,
        string BoxName);

    public readonly record struct Result(
        ResultCode Code,
        string? AgentName = null,
        string? BoxName = null);

    public enum ResultCode
    {
        Ok = 0,
        AgentNotFound,
        BoxNotFound
    }
}
