using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Workspaces.Cache;

namespace PlikShare.Workspaces.GetSize;

public class GetWorkspaceSizeQuery(PlikShareDb plikShareDb)
{
    private const string SQL = """
        WITH folders_to_delete AS (
            SELECT fo_id
            FROM fo_folders
            WHERE fo_workspace_id = $workspaceId
                AND fo_is_being_deleted = TRUE
        )
        SELECT COALESCE((
            SELECT SUM(fu_file_size_in_bytes)
            FROM fu_file_uploads
            WHERE fu_workspace_id = $workspaceId
            AND fu_folder_id NOT IN (
                SELECT fo_id FROM folders_to_delete
            )
        ), 0) + COALESCE((
            SELECT SUM(fi_size_in_bytes)
            FROM fi_files
            WHERE fi_workspace_id = $workspaceId
            AND fi_folder_id NOT IN (
                SELECT fo_id FROM folders_to_delete
            )
        ), 0)
        """;

    public long Execute(
        WorkspaceContext workspace)
    {
        using var connection = plikShareDb.OpenConnection();

        return Execute(
            workspaceId: workspace.Id, 
            connection: connection,
            transaction: null);
    }

    public static long Execute(
        int workspaceId,
        SqliteConnection connection,
        SqliteTransaction? transaction)
    {
        return connection
            .OneRowCmd(
                sql: SQL,
                readRowFunc: reader => reader.GetInt64(0),
                transaction: transaction)
            .WithParameter("$workspaceId", workspaceId)
            .ExecuteOrValue(0);
    }

    public static long Execute(
        int workspaceId,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction? transaction)
    {
        return dbWriteContext
            .OneRowCmd(
                sql: SQL,
                readRowFunc: reader => reader.GetInt64(0),
                transaction: transaction)
            .WithParameter("$workspaceId", workspaceId)
            .ExecuteOrValue(0);
    }
}