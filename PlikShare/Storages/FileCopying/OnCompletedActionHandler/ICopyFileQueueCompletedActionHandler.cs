using Microsoft.Data.Sqlite;
using PlikShare.Core.SQLite;

namespace PlikShare.Storages.FileCopying.OnCompletedActionHandler;

public interface ICopyFileQueueCompletedActionHandler
{
    string HandlerType { get; }

    //this action should probably schedule some operation on queue and that's it. no async shit or anything
    void OnCopyFileCompleted(
        SqliteWriteContext dbWriteContext,
        string actionHandlerDefinition,
        int sourceFileId,
        int sourceWorkspaceId,
        int newFileId,
        int destinationWorkspaceId,
        Guid correlationId,
        SqliteTransaction transaction);
}