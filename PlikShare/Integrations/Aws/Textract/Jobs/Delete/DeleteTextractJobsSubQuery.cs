using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;

namespace PlikShare.Integrations.Aws.Textract.Jobs.Delete;

public class DeleteTextractJobsSubQuery
{
    public List<DeletedTextractJob> Execute(
        int integrationId,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        return dbWriteContext
            .Cmd(
                sql: @"
                    DELETE FROM itj_integrations_textract_jobs
                    WHERE itj_textract_integration_id = $integrationId
                    RETURNING 
                        itj_id
                ",
                readRowFunc: reader => new DeletedTextractJob(
                    Id: reader.GetInt32(0)),
                transaction: transaction)
            .WithJsonParameter("$integrationId", integrationId)
            .Execute();
    }

    public List<DeletedTextractJob> Execute(
        int workspaceId,
        int[] deletedFileIds,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        if (deletedFileIds.Length == 0)
            return [];

        return dbWriteContext
            .Cmd(
                sql: @"
                    DELETE FROM itj_integrations_textract_jobs
                    WHERE (
                            itj_original_workspace_id = $workspaceId
                            AND itj_original_file_id IN (
                                SELECT value FROM json_each($deletedFileIds)
                            )
                        ) OR (
                            itj_textract_workspace_id = $workspaceId
                            AND itj_textract_file_id IN (
                                SELECT value FROM json_each($deletedFileIds)
                            )
                        )
                    RETURNING 
                        itj_id
                ",
                readRowFunc: reader => new DeletedTextractJob(
                    Id: reader.GetInt32(0)),
                transaction: transaction)
            .WithParameter("$workspaceId", workspaceId)
            .WithJsonParameter("$deletedFileIds", deletedFileIds)
            .Execute();
    }

    public readonly record struct DeletedTextractJob(
        int Id);
}