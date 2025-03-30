using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Files.Id;
using PlikShare.Workspaces.UpdateCurrentSizeInBytes.QueueJob;
using Serilog;

namespace PlikShare.Files.UpdateSize;

public class UpdateFileSizeQuery(
    DbWriteQueue dbWriteQueue,
    IQueue queue,
    IClock clock)
{
    public Task Execute(
        FileExtId fileExternalId,
        long newSizeInBytes,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                context,
                fileExternalId,
                newSizeInBytes,
                correlationId),
            cancellationToken: cancellationToken);
    }

    private void ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        FileExtId fileExternalId,
        long newSizeInBytes,
        Guid correlationId)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var workspaceId = dbWriteContext
                .OneRowCmd(
                    sql: @"
                    UPDATE fi_files
                    SET fi_size_in_bytes = $sizeInBytes
                    WHERE fi_external_id = $fileExternalId
                    RETURNING fi_workspace_id
                ",
                    readRowFunc: reader => reader.GetInt32(0),
                    transaction: transaction)
                .WithParameter("$fileExternalId", fileExternalId.Value)
                .WithParameter("$sizeInBytes", newSizeInBytes)
                .ExecuteOrThrow();

            queue.EnqueueWorkspaceSizeUpdateJob(
                clock: clock,
                workspaceId: workspaceId,
                correlationId: correlationId,
                dbWriteContext: dbWriteContext,
                transaction: transaction);

            transaction.Commit();
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e, "Something went wrong while updating size of File '{FileExternalId}'", fileExternalId);

            throw;
        }
    }
}