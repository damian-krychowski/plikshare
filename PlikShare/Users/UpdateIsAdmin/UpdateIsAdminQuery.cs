using Microsoft.Data.Sqlite;
using PlikShare.Core.Authorization;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.IdentityProvider;
using PlikShare.Core.SQLite;
using PlikShare.Users.Cache;
using Serilog;

namespace PlikShare.Users.UpdateIsAdmin;

public class UpdateIsAdminQuery(DbWriteQueue dbWriteQueue)
{
    public Task Execute(
        UserContext user,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                user,
                isAdmin),
            cancellationToken: cancellationToken);
    }

    private void ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        UserContext user,
        bool isAdmin)
    {
        var adminRoleId = SelectOrInsertAdminRole(
            dbWriteContext);

        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var setAdminResult = SetIsAdminRole(
                user,
                isAdmin,
                adminRoleId,
                dbWriteContext,
                transaction);

            if (setAdminResult == SetAdminRoleResult.UserWasChanged)
            {
                UpdateUserConcurrencyStamp(
                    user,
                    isAdmin,
                    dbWriteContext,
                    transaction);
            }

            transaction.Commit();
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e, "Something went wrong while setting Admin Role {IsAdmin} for User '{UserId}'",
                isAdmin,
                user.Id);

            throw;
        }
    }

    private static void UpdateUserConcurrencyStamp(
        UserContext user,
        bool isAdmin,
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
                $"Could not update User '{user.Id}' ConcurrencyStamp after his Admin role was changed to {isAdmin}");
        }

        Log.Information("User '{UserId}' ConcurrencyStamp was updated after his Admin role was changed to {IsAdmin}",
            user.Id,
            isAdmin);
    }

    private static SetAdminRoleResult SetIsAdminRole(
        UserContext user,
        bool isAdmin,
        int adminRoleId,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        if (isAdmin)
        {
            var roleAddResult = TryAddAdminRole(
                user,
                adminRoleId,
                dbWriteContext,
                transaction);

            if (roleAddResult.IsEmpty)
            {
                Log.Information("User '{UserId}' already had Admin Role '{RoleId}'",
                    user.Id,
                    adminRoleId);

                return SetAdminRoleResult.UserWasNotChanged;
            }

            Log.Information("User '{UserId}' was assigned with Admin Role '{RoleId}'",
                roleAddResult.Value.UserId,
                roleAddResult.Value.RoleId);

            return SetAdminRoleResult.UserWasChanged;
        }
        else
        {
            var roleRemoveResult = TryRemoveAdminRole(
                user,
                adminRoleId,
                dbWriteContext,
                transaction);

            if (roleRemoveResult.IsEmpty)
            {
                Log.Information("User '{UserId}' already didn't have Admin Role '{RoleId}'",
                    user.Id,
                    adminRoleId);

                return SetAdminRoleResult.UserWasNotChanged;
            }

            Log.Information("User '{UserId}' Admin Role '{RoleId}' was removed",
                roleRemoveResult.Value.UserId,
                roleRemoveResult.Value.RoleId);

            var removedAdminPermissions = RemoveAllAdminPermissions(
                user,
                dbWriteContext,
                transaction);

            if (removedAdminPermissions.Count > 0)
            {
                Log.Information(
                    "User '{UserId}' Permissions '{@Permissions}' were removed.",
                    user.Id,
                    removedAdminPermissions);
            }

            return SetAdminRoleResult.UserWasChanged;
        }
    }

    private static SQLiteOneRowCommandResult<UserRole> TryRemoveAdminRole(
        UserContext user,
        int adminRoleId,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        return dbWriteContext
            .OneRowCmd(
                sql: """
                     DELETE FROM ur_user_roles 
                     WHERE 
                         ur_user_id = $userId
                         AND ur_role_id = $roleId
                     RETURNING                        
                         ur_user_id,
                         ur_role_id
                     """,
                readRowFunc: reader => new UserRole(
                    UserId: reader.GetInt32(0),
                    RoleId: reader.GetInt32(1)),
                transaction: transaction)
            .WithParameter("$userId", user.Id)
            .WithParameter("$roleId", adminRoleId)
            .Execute();
    }

    private static List<DeletedPermission> RemoveAllAdminPermissions(
        UserContext user,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        return dbWriteContext
            .Cmd(
                sql: $"""
                      DELETE FROM uc_user_claims 
                      WHERE 
                          uc_user_id = $userId
                          AND uc_claim_type = '{Claims.Permission}'
                          AND uc_claim_value IN (
                              '{Permissions.ManageGeneralSettings}',
                              '{Permissions.ManageUsers}',
                              '{Permissions.ManageStorages}',
                              '{Permissions.ManageEmailProviders}'
                          )
                      RETURNING                        
                          uc_id,
                          uc_claim_value
                      """,
                readRowFunc: reader => new DeletedPermission(
                    UserClaimId: reader.GetInt32(0),
                    ClaimValue: reader.GetString(1)),
                transaction: transaction)
            .WithParameter("$userId", user.Id)
            .Execute();
    }

    private static SQLiteOneRowCommandResult<UserRole> TryAddAdminRole(
        UserContext user,
        int adminRoleId,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        return dbWriteContext
            .OneRowCmd(
                sql: """
                     INSERT INTO ur_user_roles (
                         ur_user_id, 
                         ur_role_id
                     ) VALUES (
                         $userId,
                         $roleId
                     )
                     ON CONFLICT (ur_user_id, ur_role_id) DO NOTHING
                     RETURNING                        
                         ur_user_id,
                         ur_role_id
                     """,
                readRowFunc: reader => new UserRole(
                    UserId: reader.GetInt32(0),
                    RoleId: reader.GetInt32(1)),
                transaction: transaction)
            .WithParameter("$userId", user.Id)
            .WithParameter("$roleId", adminRoleId)
            .Execute();
    }

    private int SelectOrInsertAdminRole(
        DbWriteQueue.Context dbWriteContext)
    {
        var firstSelectResult = SelectAdminRole(
            dbWriteContext);

        if (!firstSelectResult.IsEmpty)
            return firstSelectResult.Value;

        var insertionResult = dbWriteContext
            .OneRowCmd(
                sql: """
                     INSERT INTO r_roles (
                         r_external_id,
                         r_name,
                         r_normalized_name,
                         r_concurrency_stamp
                     ) VALUES (
                         $externalId,
                         $name,
                         $normalizedName,
                         $concurrencyStamp
                     )
                     ON CONFLICT(r_normalized_name) DO NOTHING
                     RETURNING r_id
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$externalId", RoleExtId.NewId().Value)
            .WithParameter("$name", Roles.Admin)
            .WithParameter("$normalizedName", Roles.Admin.ToUpperInvariant())
            .WithParameter("$concurrencyStamp", Guid.NewGuid())
            .Execute();

        if (!insertionResult.IsEmpty)
            return insertionResult.Value;

        //it could have been created in the meantime and insertion could have failed and do nothing
        //so we need to select for the second time
        var secondSelectResult = SelectAdminRole(
            dbWriteContext);

        if (secondSelectResult.IsEmpty)
        {
            throw new InvalidOperationException(
                "Could not select Admin role from Database");
        }

        return secondSelectResult.Value;
    }

    private static SQLiteOneRowCommandResult<int> SelectAdminRole(
        DbWriteQueue.Context dbWriteContext)
    {
        return dbWriteContext
            .OneRowCmd(
                sql: """
                     SELECT r_id
                     FROM r_roles
                     WHERE r_normalized_name = $normalizedName
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$normalizedName", Roles.Admin.ToUpperInvariant())
            .Execute();
    }

    private enum SetAdminRoleResult
    {
        UserWasChanged,
        UserWasNotChanged
    }

    private readonly record struct UserRole(
        int UserId,
        int RoleId);

    private readonly record struct DeletedPermission(
        int UserClaimId,
        string ClaimValue);
}