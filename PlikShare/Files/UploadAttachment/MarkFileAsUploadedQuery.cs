using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Files.Id;
using Serilog;

namespace PlikShare.Files.UploadAttachment;

public class MarkFileAsUploadedQuery(
    DbWriteQueue dbWriteQueue)
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<MarkFileAsUploadedQuery>();

    public Task Execute(
        FileExtId fileExternalId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteInTransaction(
                dbWriteContext: context,
                transaction: null,
                fileExternalId: fileExternalId),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Sub-query form: runs the UPDATE inside a caller-owned transaction (or no transaction
    /// when <paramref name="transaction"/> is null — used by the top-level <see cref="Execute"/>
    /// path where the queue serializes the write). Compose this from operations that need to
    /// mark a file uploaded atomically with other writes (e.g. thumbnail finalize that also
    /// hard-deletes the replaced thumb in the same commit).
    /// </summary>
    public void ExecuteInTransaction(
        SqliteWriteContext dbWriteContext,
        SqliteTransaction? transaction,
        FileExtId fileExternalId)
    {
        try
        {
            var fileId = dbWriteContext
                .OneRowCmd(
                    sql: @"
                        UPDATE fi_files
                        SET fi_is_upload_completed = TRUE
                        WHERE fi_external_id = $fileExternalId
                        RETURNING fi_id",
                    readRowFunc: reader => reader.GetInt32(0),
                    transaction: transaction)
                .WithParameter("$fileExternalId", fileExternalId.Value)
                .Execute();

            if (fileId.IsEmpty)
            {
                Logger.Warning(
                    "Failed to update file upload status. File not found. FileExternalId: {FileExternalId}",
                    fileExternalId.Value);
            }
            else
            {
                Logger.Information(
                    "Successfully completed marking file as uploaded. FileExternalId: {FileExternalId}",
                    fileExternalId.Value);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(
                ex,
                "Failed to mark file as uploaded. FileExternalId: {FileExternalId}",
                fileExternalId.Value);

            throw;
        }
    }
}
