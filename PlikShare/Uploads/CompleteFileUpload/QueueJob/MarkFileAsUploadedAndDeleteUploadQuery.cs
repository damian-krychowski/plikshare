using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Files.Id;
using Serilog;

namespace PlikShare.Uploads.CompleteFileUpload.QueueJob;

public class MarkFileAsUploadedAndDeleteUploadQuery(
    DbWriteQueue dbWriteQueue)
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<MarkFileAsUploadedAndDeleteUploadQuery>();

    public Task Execute(
        int fileUploadId,
        FileExtId fileExternalId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                fileUploadId,
                fileExternalId),
            cancellationToken: cancellationToken);
    }

    private void ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        int fileUploadId,
        FileExtId fileExternalId)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            Logger.Debug(
                "Starting to mark file as uploaded and delete file upload. FileUploadId: {FileUploadId}, FileExternalId: {FileExternalId}",
                fileUploadId,
                fileExternalId);

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
                    "Failed to update file upload status. File not found. FileUploadId: {FileUploadId}, FileExternalId: {FileExternalId}",
                    fileUploadId,
                    fileExternalId.Value);
            }
            else
            {
                Logger.Debug(
                    "Successfully marked file as uploaded. FileId: {FileId}, FileExternalId: {FileExternalId}",
                    fileId.Value,
                    fileExternalId.Value);
            }

            var deletedParts = dbWriteContext
                .Cmd(
                    sql: @"
                        DELETE FROM fup_file_upload_parts
                        WHERE fup_file_upload_id = $fileUploadId
                        RETURNING fup_part_number",
                    readRowFunc: reader => reader.GetInt32(0),
                    transaction: transaction)
                .WithParameter("$fileUploadId", fileUploadId)
                .Execute()
                .ToList();

            Logger.Debug(
                "Deleted file upload parts. FileUploadId: {FileUploadId}, DeletedPartCount: {DeletedPartCount}, DeletedParts: {@DeletedParts}",
                fileUploadId,
                deletedParts.Count,
                deletedParts);

            var deletedUploadId = dbWriteContext
                .OneRowCmd(
                    sql: @"
                        DELETE FROM fu_file_uploads
                        WHERE fu_id = $fileUploadId
                        RETURNING fu_id",
                    readRowFunc: reader => reader.GetInt32(0),
                    transaction: transaction)
                .WithParameter("$fileUploadId", fileUploadId)
                .Execute();

            if (deletedUploadId.IsEmpty)
            {
                Logger.Warning(
                    "Failed to delete file upload. FileUpload not found. FileUploadId: {FileUploadId}",
                    fileUploadId);
            }
            else
            {
                Logger.Debug(
                    "Successfully deleted file upload. FileUploadId: {FileUploadId}",
                    fileUploadId);
            }

            transaction.Commit();

            Logger.Information(
                "Successfully completed marking file as uploaded and deleting file upload. FileUploadId: {FileUploadId}, FileExternalId: {FileExternalId}",
                fileUploadId,
                fileExternalId.Value);
        }
        catch (Exception ex)
        {
            Logger.Error(
                ex,
                "Failed to mark file as uploaded and delete file upload. FileUploadId: {FileUploadId}, FileExternalId: {FileExternalId}",
                fileUploadId,
                fileExternalId.Value);

            transaction.Rollback();
        }
    }
}