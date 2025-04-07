using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Users.Cache;
using Serilog;

namespace PlikShare.Users.UpdateDefaultMaxWorkspaceTeamMembers;

public class UpdateUserDefaultMaxWorkspaceTeamMembersQuery(DbWriteQueue dbWriteQueue)
{
    public Task Execute(
        UserContext user,
        int? defaultMaxWorkspaceTeamMembers,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                user,
                defaultMaxWorkspaceTeamMembers),
            cancellationToken: cancellationToken);
    }

    private void ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        UserContext user,
        int? defaultMaxWorkspaceTeamMembers)
    {
        var result = dbWriteContext
          .OneRowCmd(
              sql: """
                     UPDATE u_users
                     SET
                        u_default_max_workspace_team_members = $defaultMaxWorkspaceTeamMembers,
                        u_concurrency_stamp = $concurrencyStamp
                     WHERE u_id = $userId
                     RETURNING u_id
                     """,
              readRowFunc: reader => reader.GetInt32(0))
          .WithParameter("$defaultMaxWorkspaceTeamMembers", defaultMaxWorkspaceTeamMembers)
          .WithParameter("$concurrencyStamp", Guid.NewGuid())
          .WithParameter("$userId", user.Id)
          .Execute();

        if (result.IsEmpty)
        {
            throw new InvalidOperationException(
                $"Could not update User#{user.Id} DefaultMaxWorkspaceTeamMembers to {defaultMaxWorkspaceTeamMembers?.ToString() ?? "NULL"}.");
        }

        Log.Information("User#{UserId} DefaultMaxWorkspaceTeamMembers was updated to {DefaultMaxWorkspaceTeamMembers}.",
            user.Id,
            defaultMaxWorkspaceTeamMembers);
    }
}