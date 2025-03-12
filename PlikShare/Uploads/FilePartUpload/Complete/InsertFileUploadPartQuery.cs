using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.Uploads.FilePartUpload.Complete;

public class InsertFileUploadPartQuery(
    DbWriteQueue dbWriteQueue)
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<InsertFileUploadPartQuery>();

    public Task<Result> Execute(
        int fileUploadId,
        int partNumber,
        string eTag,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context, 
                fileUploadId: fileUploadId, 
                partNumber: partNumber, 
                eTag: eTag),
            cancellationToken: cancellationToken);
    }

    private Result ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        int fileUploadId,
        int partNumber,
        string eTag)
    {
        try
        {
            var result = dbWriteContext
                .OneRowCmd(
                    sql: @"
                        INSERT INTO fup_file_upload_parts (
                            fup_file_upload_id,
                            fup_part_number,
                            fup_etag
                        ) VALUES(
                            $fileUploadId,
                            $partNumber,
                            $etag
                        )
                        RETURNING 
                            fup_file_upload_id",
                    readRowFunc: reader => reader.GetInt32(0))
                .WithParameter("$fileUploadId", fileUploadId)
                .WithParameter("$partNumber", partNumber)
                .WithParameter("$etag", eTag)
                .Execute();

            if (result.IsEmpty)
            {
                Logger.Error("Failed to insert file upload part - no rows returned. FileUploadId: {FileUploadId}, PartNumber: {PartNumber}",
                    fileUploadId, partNumber);

                throw new InvalidOperationException(
                    $"Something went wrong while inserting file upload part for file upload '{fileUploadId}' and part '{partNumber}'");
            }

            Logger.Debug(
                "File part FileUploadId: {FileUploadId}, PartNumber: {PartNumber} was uploaded.",
                fileUploadId,
                partNumber);

            return new Result(
                Code: ResultCode.Ok,
                Details: new Details(
                    FileUploadId: fileUploadId,
                    PartNumber: partNumber));
        }
        catch (SqliteException ex) when (ex.HasForeignKeyFailed())
        {
            Logger.Warning(ex,
                "Foreign Key constraint failed while completing FileUploadPart FileUploadId: '{FileUploadId}' PartNumber: '{PartNumber}'",
                fileUploadId,
                partNumber);

            return new Result(
                Code: ResultCode.FileUploadNotFound,
                Details: new Details(
                    FileUploadId: fileUploadId,
                    PartNumber: partNumber));
        }
        catch (Exception ex)
        {
            Logger.Error(ex,
                "Database error while inserting file upload part. FileUploadId: {FileUploadId}, PartNumber: {PartNumber}",
                fileUploadId,
                partNumber);

            throw;
        }
    }

    public record Result(
        ResultCode Code,
        Details Details);

    public readonly record struct Details(
        int FileUploadId,
        int PartNumber);

    public enum ResultCode
    {
        Ok,
        FileUploadNotFound
    }
}