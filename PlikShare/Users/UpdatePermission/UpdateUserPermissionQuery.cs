using Microsoft.Data.Sqlite;
using PlikShare.Core.Authorization;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Users.Cache;
using Serilog;

namespace PlikShare.Users.UpdatePermission;

public class UpdateUserPermissionQuery(DbWriteQueue dbWriteQueue)
{
    public Task Execute(
        UserContext user,
        Operation operation,
        string permissionName,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                user,
                operation,
                permissionName),
            cancellationToken: cancellationToken);
    }

    private void ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        UserContext user,
        Operation operation,
        string permissionName)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var setAdminResult = SetPermission(
                user,
                operation,
                permissionName,
                dbWriteContext,
                transaction);

            if (setAdminResult == SetPermissionResult.UserWasChanged)
            {
                UpdateUserConcurrencyStamp(
                    user,
                    permissionName,
                    dbWriteContext,
                    transaction);
            }

            transaction.Commit();
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e, "Something went wrong while '{Operation}' setting Permission {Permission} for User '{UserId}'",
                operation,
                permissionName,
                user.Id);

            throw;
        }
    }

    private static SetPermissionResult SetPermission(
        UserContext user,
        Operation operation,
        string permissionName,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        switch (operation)
        {
            case Operation.AddPermission:
                {
                    var permissionAddResult = TryAddPermission(
                        user,
                        permissionName,
                        dbWriteContext,
                        transaction);

                    if (permissionAddResult.IsEmpty)
                    {
                        Log.Information("User '{UserId}' already had Permission '{PermissionName}'",
                            user.Id,
                            permissionName);

                        return SetPermissionResult.UserWasNotChanged;
                    }

                    Log.Information("User '{UserId}' was assigned with Permission '{PermissionName}'. " +
                                    "User Claim '{UserClaimId}' was created",
                        user.Id,
                        permissionName,
                        permissionAddResult.Value);

                    return SetPermissionResult.UserWasChanged;
                }

            case Operation.RemovePermission:
                {
                    var removeResult = TryRemovePermission(
                        user,
                        permissionName,
                        dbWriteContext,
                        transaction);

                    if (removeResult.IsEmpty)
                    {
                        Log.Information("User '{UserId}' already didn't have Permission '{PermissionName}'",
                            user.Id,
                            permissionName);

                        return SetPermissionResult.UserWasNotChanged;
                    }

                    Log.Information("User '{UserId}' Permission '{PermissionName}'was removed. " +
                                    "User Claim '{UserClaimId}' was removed.",
                        user.Id,
                        permissionName,
                        removeResult.Value);

                    return SetPermissionResult.UserWasChanged;
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
        }
    }

    private static SQLiteOneRowCommandResult<int> TryRemovePermission(
        UserContext user,
        string permissionName,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        return dbWriteContext
            .OneRowCmd(
                sql: """
                     DELETE FROM uc_user_claims 
                     WHERE 
                         uc_user_id = $userId
                         AND uc_claim_type = $claimType
                         AND uc_claim_value = $claimValue
                     RETURNING                        
                         uc_id
                     """,
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$userId", user.Id)
            .WithParameter("$claimType", Claims.Permission)
            .WithParameter("$claimValue", permissionName)
            .Execute();
    }

    private static SQLiteOneRowCommandResult<int> TryAddPermission(
        UserContext user,
        string permissionName,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        return dbWriteContext
            .OneRowCmd(
                sql: """
                     INSERT INTO uc_user_claims (
                         uc_user_id,
                         uc_claim_type,
                         uc_claim_value
                     ) VALUES (
                         $userId,
                         $claimType,
                         $claimValue
                     )
                     ON CONFLICT (uc_user_id, uc_claim_type, uc_claim_value) DO NOTHING
                     RETURNING                        
                         uc_id
                     """,
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$userId", user.Id)
            .WithParameter("$claimType", Claims.Permission)
            .WithParameter("$claimValue", permissionName)
            .Execute();
    }

    private static void UpdateUserConcurrencyStamp(
        UserContext user,
        string permissionName,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: """
                     UPDATE u_users
                     SET u_concurrency_stamp = $concurrencyStamp
                     WHERE u_id = $userId
                     RETURNING u_id
                     """,
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$concurrencyStamp", Guid.NewGuid())
            .WithParameter("$userId", user.Id)
            .Execute();

        if (result.IsEmpty)
        {
            throw new InvalidOperationException(
                $"Could not update User '{user.Id}' ConcurrencyStamp after his Permission '{permissionName}' has changed.");
        }

        Log.Information("User '{UserId}' ConcurrencyStamp was updated after his Permission '{PermissionName}' has changed.",
            user.Id,
            permissionName);
    }

    public enum Operation
    {
        AddPermission,
        RemovePermission
    }

    private enum SetPermissionResult
    {
        UserWasChanged,
        UserWasNotChanged
    }
}