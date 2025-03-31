using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Users.Cache;
using Serilog;

namespace PlikShare.Users.UpdateDefaultMaxWorkspaceSizeInBytes;

public class UpdateUserDefaultMaxWorkspaceSizeInBytesQuery(DbWriteQueue dbWriteQueue)
{
    public Task Execute(
        UserContext user,
        long? defaultMaxWorkspaceSizeInBytes,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                user,
                defaultMaxWorkspaceSizeInBytes),
            cancellationToken: cancellationToken);
    }

    private void ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        UserContext user,
        long? defaultMaxWorkspaceSizeInBytes)
    {
        var result = dbWriteContext
          .OneRowCmd(
              sql: """
                     UPDATE u_users
                     SET
                        u_default_max_workspace_size_in_bytes = $defaultMaxWorkspaceSizeInBytes,
                        u_concurrency_stamp = $concurrencyStamp
                     WHERE u_id = $userId
                     RETURNING u_id
                     """,
              readRowFunc: reader => reader.GetInt32(0))
          .WithParameter("$defaultMaxWorkspaceSizeInBytes", defaultMaxWorkspaceSizeInBytes)
          .WithParameter("$concurrencyStamp", Guid.NewGuid())
          .WithParameter("$userId", user.Id)
          .Execute();

        if (result.IsEmpty)
        {
            throw new InvalidOperationException(
                $"Could not update User '{user.Id}' DefaultMaxWorkspaceSizeInBytes to {defaultMaxWorkspaceSizeInBytes?.ToString() ?? "NULL"}.");
        }

        Log.Information("User '{UserId}' DefaultMaxWorkspaceSizeInBytes was updated to {DefaultMaxWorkspaceSizeInBytes}.",
            user.Id,
            defaultMaxWorkspaceSizeInBytes);
    }
}