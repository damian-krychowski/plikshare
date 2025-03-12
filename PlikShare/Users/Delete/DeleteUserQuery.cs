using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Users.Cache;
using Serilog;

namespace PlikShare.Users.Delete;

public class DeleteUserQuery(DbWriteQueue dbWriteQueue)
{
    public Task<Result> Execute(
        UserContext user,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                user),
            cancellationToken: cancellationToken);
    }

    private Result ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        UserContext user)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var deletedBoxMemberships = dbWriteContext
                .Cmd(
                    sql: """
                         DELETE FROM bm_box_membership
                         WHERE bm_member_id = $userId
                         RETURNING bm_box_id 
                         """,
                    readRowFunc: reader => reader.GetInt32(0),
                    transaction: transaction)
                .WithParameter("$userId", user.Id)
                .Execute();

            var deletedWorkspaceMemberships = dbWriteContext
                .Cmd(
                    sql: """
                         DELETE From wm_workspace_membership
                         WHERE wm_member_id = $userId
                         RETURNING wm_workspace_id
                         """,
                    readRowFunc: reader => reader.GetInt32(0),
                    transaction: transaction)
                .WithParameter("$userId", user.Id)
                .Execute();

            var deletedUserClaims = dbWriteContext
                .Cmd(
                    sql: """
                         DELETE FROM uc_user_claims
                         WHERE uc_user_id = $userId
                         RETURNING
                             uc_claim_type,
                             uc_claim_value
                         """,
                    readRowFunc: reader => new
                    {
                        ClaimType = reader.GetString(0),
                        ClaimValue = reader.GetString(1)
                    },
                    transaction: transaction)
                .WithParameter("$userId", user.Id)
                .Execute();

            var deletedUserLogins = dbWriteContext
                .Cmd(
                    sql: """
                         DELETE FROM ul_user_logins
                         WHERE ul_user_id = $userId
                         RETURNING
                             ul_login_provider
                         """,
                    readRowFunc: reader => new
                    {
                        LoginProvider = reader.GetString(0)
                    },
                    transaction: transaction)
                .WithParameter("$userId", user.Id)
                .Execute();

            var deletedUserRoles = dbWriteContext
                .Cmd(
                    sql: """
                         DELETE FROM ur_user_roles
                         WHERE ur_user_id = $userId
                         RETURNING
                             ur_role_id
                         """,
                    readRowFunc: reader => new
                    {
                        RoleId = reader.GetInt32(0)
                    },
                    transaction: transaction)
                .WithParameter("$userId", user.Id)
                .Execute();

            var deletedUserTokens = dbWriteContext
                .Cmd(
                    sql: """
                         DELETE FROM ut_user_tokens
                         WHERE ut_user_id = $userId
                         RETURNING
                             ut_login_provider,
                             ut_name
                         """,
                    readRowFunc: reader => new
                    {
                        LoginProvider = reader.GetString(0),
                        Name = reader.GetString(1)
                    },
                    transaction: transaction)
                .WithParameter("$userId", user.Id)
                .Execute();

            var deletedUser = dbWriteContext
                .OneRowCmd(
                    sql: """
                         DELETE FROM u_users
                         WHERE u_id = $userId
                         RETURNING u_id
                         """,
                    readRowFunc: reader => reader.GetInt32(0),
                    transaction: transaction)
                .WithParameter("$userId", user.Id)
                .Execute();

            if (deletedUser.IsEmpty)
            {
                transaction.Rollback();

                Log.Warning("User '{UserExternalId}' was not found.",
                    user.ExternalId);

                return new Result(
                    Code: ResultCode.NotFound);
            }

            transaction.Commit();

            Log.Information("User '{UserExternalId}' was deleted. Query result: {@QueryResult}",
                user.ExternalId,
                new
                {
                    deletedBoxMemberships,
                    deletedWorkspaceMemberships,
                    deletedUserRoles,
                    deletedUserLogins,
                    deletedUserClaims,
                    deletedUserTokens
                });

            return new Result(
                ResultCode.Ok,
                DeletedBoxMemberships: deletedBoxMemberships,
                DeletedWorkspaceMemberships: deletedWorkspaceMemberships);
        }
        catch (SqliteException e)
        {
            transaction.Rollback();

            if (e.HasForeignKeyFailed())
            {
                Log.Error(e, "Foreign Key constraint failed while deleting User '{UserExternalId}' - there must be some outstanding dependencies left (like Workspaces)",
                    user.ExternalId);

                return new Result(
                    Code: ResultCode.UserHasOutstandingDependencies);
            }

            throw;
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e, "Something went wrong while deleting User '{UserExternalId}'",
                user.ExternalId);

            throw;
        }
    }

    public enum ResultCode
    {
        Ok = 0,
        NotFound,
        UserHasOutstandingDependencies
    }

    public record Result(
        ResultCode Code,
        List<int>? DeletedBoxMemberships = null,
        List<int>? DeletedWorkspaceMemberships = null);
}