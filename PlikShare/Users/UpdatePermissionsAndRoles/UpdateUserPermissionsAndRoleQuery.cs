using Microsoft.Data.Sqlite;
using PlikShare.Core.Authorization;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.GeneralSettings;
using PlikShare.Users.PermissionsAndRoles;
using Serilog;

namespace PlikShare.Users.UpdatePermissionsAndRoles;

public class UpdateUserPermissionsAndRoleQuery(
    DbWriteQueue dbWriteQueue,
    AppSettings appSettings)
{
    public Task Execute(
        int targetUserId,
        UserPermissionsAndRolesDto request,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                targetUserId: targetUserId,
                request: request),
            cancellationToken: cancellationToken);
    }

    private void ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        int targetUserId,
        UserPermissionsAndRolesDto request)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var originalRoles = RemoveRoles(
                userId: targetUserId,
                adminRoleId: appSettings.AdminRoleId,
                dbWriteContext: dbWriteContext,
                transaction: transaction);

            var originalPermissions = RemoveAllPermissions(
                userId: targetUserId,
                dbWriteContext: dbWriteContext,
                transaction: transaction);

            if (request.IsAdmin)
            {
                AddAdminRole(
                    userId: targetUserId,
                    adminRoleId: appSettings.AdminRoleId,
                    dbWriteContext: dbWriteContext,
                    transaction: transaction);
            }

            var permissions = request.GetPermissionsList();

            if (permissions.Count > 0)
            {
                AddPermissions(
                    userId: targetUserId,
                    isAdmin: request.IsAdmin,
                    permissions: permissions,
                    dbWriteContext: dbWriteContext,
                    transaction: transaction);
            }

            UpdateUserConcurrencyStamp(
                targetUserId,
                dbWriteContext,
                transaction);

            transaction.Commit();

            Log.Information(
                "User#{UserId} permissions and roles updated. Original: [IsAdmin: {OriginalIsAdmin}, Permissions: {OriginalPermissions}]. " +
                "Current: [IsAdmin: {CurrentIsAdmin}, Permissions: {CurrentPermissions}]",
                targetUserId,
                originalRoles.IsAdmin,
                string.Join(", ", originalPermissions),
                request.IsAdmin,
                string.Join(", ", permissions));
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e, "Something went wrong while updating permissions and roles for User '{UserId}'",
                targetUserId);

            throw;
        }
    }
       
    private static OriginalRoles RemoveRoles(
        int userId,
        int adminRoleId,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: """
                     DELETE FROM ur_user_roles 
                     WHERE 
                         ur_user_id = $userId
                         AND ur_role_id = $roleId
                     RETURNING      
                         ur_role_id
                     """,
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$userId", userId)
            .WithParameter("$roleId", adminRoleId)
            .Execute();

        if (result.IsEmpty) return new(IsAdmin: false);
        return new(IsAdmin: true);
    }

    private static List<string> RemoveAllPermissions(
        int userId,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        return dbWriteContext
            .Cmd(
                sql: """
                     DELETE FROM uc_user_claims 
                     WHERE 
                         uc_user_id = $userId
                         AND uc_claim_type = $claimType
                     RETURNING                        
                         uc_claim_value
                     """,
                readRowFunc: reader => reader.GetString(0),
                transaction: transaction)
            .WithParameter("$userId", userId)
            .WithParameter("$claimType", Claims.Permission)
            .Execute();
    }

    public static void AddAdminRole(
        int userId,
        int adminRoleId,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        dbWriteContext
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
                         ur_user_id
                     """,
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$userId", userId)
            .WithParameter("$roleId", adminRoleId)
            .Execute();
    }

    public static void AddPermissions(
        int userId,
        bool isAdmin,
        List<string> permissions,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        foreach (var permission in permissions)
        {
            if (Permissions.IsForAdminOnly(permission) && !isAdmin)
                continue;

            dbWriteContext
                .OneRowCmd(
                    sql: """
                     INSERT INTO uc_user_claims (
                         uc_user_id,
                         uc_claim_type,
                         uc_claim_value
                     ) 
                     VALUES (
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
                .WithParameter("$userId", userId)
                .WithParameter("$claimType", Claims.Permission)
                .WithParameter("$claimValue", permission)
                .Execute();
        }
    }

    private static void UpdateUserConcurrencyStamp(
        int userId,
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
            .WithParameter("$userId", userId)
            .Execute();

        if (result.IsEmpty)
        {
            throw new InvalidOperationException(
                $"Could not update User#{userId} ConcurrencyStamp after his Permissions and Roles have changed.");
        }

        Log.Information("User#{UserId} ConcurrencyStamp was updated after his Permissions and Roles have changed.",
            userId);
    }

    private readonly record struct OriginalRoles(
        bool IsAdmin);
}