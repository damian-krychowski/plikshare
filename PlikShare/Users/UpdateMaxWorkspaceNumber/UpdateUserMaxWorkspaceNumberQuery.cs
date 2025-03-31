using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Users.Cache;
using Serilog;

namespace PlikShare.Users.UpdateMaxWorkspaceNumber;

public class UpdateUserMaxWorkspaceNumberQuery(DbWriteQueue dbWriteQueue)
{
    public Task Execute(
        UserContext user,
        int? maxWorkspaceNumber,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                user,
                maxWorkspaceNumber),
            cancellationToken: cancellationToken);
    }

    private void ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        UserContext user,
        int? maxWorkspaceNumber)
    {
        var result = dbWriteContext
          .OneRowCmd(
              sql: """
                     UPDATE u_users
                     SET
                        u_max_workspace_number = $maxWorkspaceNumber,
                        u_concurrency_stamp = $concurrencyStamp
                     WHERE u_id = $userId
                     RETURNING u_id
                     """,
              readRowFunc: reader => reader.GetInt32(0))
          .WithParameter("$maxWorkspaceNumber", maxWorkspaceNumber)
          .WithParameter("$concurrencyStamp", Guid.NewGuid())
          .WithParameter("$userId", user.Id)
          .Execute();

        if (result.IsEmpty)
        {
            throw new InvalidOperationException(
                $"Could not update User '{user.Id}' MaxWorkspaceNumber to {maxWorkspaceNumber?.ToString() ?? "NULL"}.");
        }

        Log.Information("User '{UserId}' MaxWorkspaceNumber was updated to {MaxWorkspaceNumber}.",
            user.Id,
            maxWorkspaceNumber);
    }
}