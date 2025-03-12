using PlikShare.Boxes.Cache;
using PlikShare.Boxes.Delete.QueueJob;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.Boxes.Delete;

public class ScheduleBoxesDeleteQuery(
    IClock clock,
    IQueue queue,
    DbWriteQueue dbWriteQueue)
{
    public Task<ResultCode> Execute(
        BoxContext box,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                box: box,
                correlationId: correlationId),
            cancellationToken: cancellationToken);
    }

    private ResultCode ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        BoxContext box,
        Guid correlationId)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();
        
        try
        {
            var deletedBoxId = dbWriteContext
                .OneRowCmd(
                    sql: """
                         UPDATE bo_boxes
                         SET bo_is_being_deleted = TRUE
                         WHERE bo_id = $boxId
                         RETURNING bo_id                        
                         """,
                    readRowFunc: reader => reader.GetInt32(0),
                    transaction: transaction)
                .WithParameter("$boxId", box.Id)
                .Execute();
            
            if (deletedBoxId.IsEmpty)
            {
                transaction.Rollback();

                Log.Warning(
                    "Could not schedule delete of Box '{BoxExternalId}' because Box was not found.",
                    box.ExternalId);

                return ResultCode.BoxesNotFound;
            }

            var deleteBoxesJob = queue.EnqueueOrThrow(
                jobType: DeleteBoxesQueueJobType.Value,
                correlationId: correlationId,
                definition: new DeleteBoxesQueueJobDefinition(
                    BoxIds: [box.Id],
                    WorkspaceId: box.Workspace.Id),
                executeAfterDate: clock.UtcNow,
                debounceId: null,
                sagaId: null,
                dbWriteContext: dbWriteContext,
                transaction: transaction);
            
            transaction.Commit();

            Log.Information("Box '{BoxExternalId}' was scheduled to be deleted. " +
                            "Query result: '{@QueryResult}'",
                box.ExternalId,
                new
                {
                    deletedBoxId,
                    deleteBoxesJob
                });

            return ResultCode.Ok;
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e, "Something went wrong while scheduling Box '{BoxExternalId}' to be deleted",
                box.ExternalId);
            
            throw;
        }
    }

    public enum ResultCode
    {
        Ok = 0,
        BoxesNotFound
    }
}