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
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                fileExternalId),
            cancellationToken: cancellationToken);
    }

    private void ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
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
                    readRowFunc: reader => reader.GetInt32(0))
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