using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.GeneralSettings;
using PlikShare.Users.Entities;
using PlikShare.Users.Id;
using PlikShare.Users.UpdatePermissionsAndRoles;
using Serilog;

namespace PlikShare.Auth.Sso;

public class GetOrCreateSsoUserQuery(
    DbWriteQueue dbWriteQueue,
    AppSettings appSettings)
{
    public Task<Result> Execute(
        Email email,
        string loginProvider,
        string providerKey,
        string providerDisplayName,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                email: email,
                loginProvider: loginProvider,
                providerKey: providerKey,
                providerDisplayName: providerDisplayName),
            cancellationToken: cancellationToken);
    }

    private Result ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        Email email,
        string loginProvider,
        string providerKey,
        string providerDisplayName)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var existingUser = TrySelectUser(
                email,
                dbWriteContext, 
                transaction);

            if (!existingUser.IsEmpty)
            {
                ConfirmEmailIfNeeded(
                    userId: existingUser.Value.Id,
                    dbWriteContext: dbWriteContext,
                    transaction: transaction);

                LinkLogin(
                    userId: existingUser.Value.Id,
                    loginProvider: loginProvider,
                    providerKey: providerKey,
                    providerDisplayName: providerDisplayName,
                    dbWriteContext: dbWriteContext,
                    transaction: transaction);

                transaction.Commit();
                return new Result(Code: ResultCode.ExistingUser, User: existingUser.Value);
            }

            if (appSettings.ApplicationSignUp != AppSettings.SignUpSetting.Everyone)
            {
                transaction.Rollback();
                return new Result(
                    Code: ResultCode.RegistrationNotAllowed);
            }

            var newUser = TryInsertSsoUser(
                email, 
                dbWriteContext, 
                transaction);

            if (!newUser.IsEmpty)
            {
                ApplyDefaultPermissions(
                    userId: newUser.Value.Id,
                    dbWriteContext: dbWriteContext,
                    transaction: transaction);

                LinkLogin(
                    userId: newUser.Value.Id,
                    loginProvider: loginProvider,
                    providerKey: providerKey,
                    providerDisplayName: providerDisplayName,
                    dbWriteContext: dbWriteContext,
                    transaction: transaction);

                transaction.Commit();

                Log.Information(
                    "SSO user '{UserEmail}' was created.",
                    email.Anonymize());

                return new Result(
                    Code: ResultCode.NewUserCreated, 
                    User: newUser.Value);
            }

            // User was created concurrently, try to select again
            existingUser = TrySelectUser(
                email, 
                dbWriteContext, 
                transaction);

            if (existingUser.IsEmpty)
            {
                throw new InvalidOperationException(
                    $"Cannot create nor select SSO user with email '{email.Anonymize()}'");
            }

            ConfirmEmailIfNeeded(
                userId: existingUser.Value.Id,
                dbWriteContext: dbWriteContext,
                transaction: transaction);

            LinkLogin(
                userId: existingUser.Value.Id,
                loginProvider: loginProvider,
                providerKey: providerKey,
                providerDisplayName: providerDisplayName,
                dbWriteContext: dbWriteContext,
                transaction: transaction);

            transaction.Commit();

            return new Result(
                Code: ResultCode.ExistingUser,
                User: existingUser.Value);
        }
        catch (Exception e) when (e is not InvalidOperationException)
        {
            transaction.Rollback();

            Log.Error(
                e,
                "Something went wrong while getting or creating SSO user '{UserEmail}'",
                email.Anonymize());

            throw;
        }
    }

    private static void ConfirmEmailIfNeeded(
        int userId,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        dbWriteContext
            .OneRowCmd(
                sql: """
                     UPDATE u_users
                     SET u_email_confirmed = TRUE,
                         u_is_invitation = FALSE
                     WHERE u_id = $userId
                       AND u_email_confirmed = FALSE
                     RETURNING u_id
                     """,
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$userId", userId)
            .Execute();
    }

    private static void LinkLogin(
        int userId,
        string loginProvider,
        string providerKey,
        string providerDisplayName,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        dbWriteContext
            .OneRowCmd(
                sql: """
                     INSERT INTO ul_user_logins (
                         ul_login_provider,
                         ul_provider_key,
                         ul_provider_display_name,
                         ul_user_id
                     ) VALUES (
                         $loginProvider,
                         $providerKey,
                         $providerDisplayName,
                         $userId
                     )
                     ON CONFLICT(ul_login_provider, ul_provider_key) DO NOTHING
                     RETURNING ul_user_id
                     """,
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$loginProvider", loginProvider)
            .WithParameter("$providerKey", providerKey)
            .WithParameter("$providerDisplayName", providerDisplayName)
            .WithParameter("$userId", userId)
            .Execute();
    }

    private void ApplyDefaultPermissions(
        int userId,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        var permissionsAndRoles = appSettings
            .NewUserDefaultPermissionsAndRoles;

        if (permissionsAndRoles.IsAdmin)
        {
            UpdateUserPermissionsAndRoleQuery.AddAdminRole(
                userId: userId,
                adminRoleId: appSettings.AdminRoleId,
                dbWriteContext: dbWriteContext,
                transaction: transaction);
        }

        var defaultPermissions = permissionsAndRoles
            .GetPermissions();

        if (defaultPermissions.Any())
        {
            UpdateUserPermissionsAndRoleQuery.AddPermissions(
                userId: userId,
                isAdmin: permissionsAndRoles.IsAdmin,
                permissions: defaultPermissions,
                dbWriteContext: dbWriteContext,
                transaction: transaction);
        }
    }

    private SQLiteOneRowCommandResult<SsoUser> TryInsertSsoUser(
        Email email,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        var externalId = UserExtId.NewId();
        var normalizedEmail = email.Normalized;
        var securityStamp = Guid.NewGuid().ToString();
        var concurrencyStamp = Guid.NewGuid().ToString();

        var maxWorkspaceNumber = appSettings.NewUserDefaultMaxWorkspaceNumber.Value;
        var defaultMaxWorkspaceSizeInBytes = appSettings.NewUserDefaultMaxWorkspaceSizeInBytes.Value;
        var defaultMaxWorkspaceTeamMembers = appSettings.NewUserDefaultMaxWorkspaceTeamMembers.Value;

        return dbWriteContext
            .OneRowCmd(
                sql: """
                     INSERT INTO u_users (
                         u_external_id,
                         u_user_name,
                         u_normalized_user_name,
                         u_email,
                         u_normalized_email,
                         u_email_confirmed,
                         u_password_hash,
                         u_security_stamp,
                         u_concurrency_stamp,
                         u_phone_number,
                         u_phone_number_confirmed,
                         u_two_factor_enabled,
                         u_lockout_end,
                         u_lockout_enabled,
                         u_access_failed_count,
                         u_is_invitation,
                         u_invitation_code,
                         u_max_workspace_number,
                         u_default_max_workspace_size_in_bytes,
                         u_default_max_workspace_team_members
                     ) VALUES (
                         $externalId,
                         $userName,
                         $normalizedUserName,
                         $email,
                         $normalizedEmail,
                         TRUE,
                         NULL,
                         $securityStamp,
                         $concurrencyStamp,
                         NULL,
                         FALSE,
                         FALSE,
                         NULL,
                         FALSE,
                         0,
                         FALSE,
                         NULL,
                         $maxWorkspaceNumber,
                         $defaultMaxWorkspaceSizeInBytes,
                         $defaultMaxWorkspaceTeamMembers
                     )
                     ON CONFLICT(u_normalized_email) DO NOTHING
                     RETURNING u_id
                     """,
                readRowFunc: reader => new SsoUser
                {
                    Id = reader.GetInt32(0),
                    ExternalId = externalId,
                    Email = email
                },
                transaction: transaction)
            .WithParameter("$externalId", externalId.Value)
            .WithParameter("$userName", email.Value)
            .WithParameter("$normalizedUserName", normalizedEmail)
            .WithParameter("$email", email.Value)
            .WithParameter("$normalizedEmail", normalizedEmail)
            .WithParameter("$securityStamp", securityStamp)
            .WithParameter("$concurrencyStamp", concurrencyStamp)
            .WithParameter("$maxWorkspaceNumber", maxWorkspaceNumber)
            .WithParameter("$defaultMaxWorkspaceSizeInBytes", defaultMaxWorkspaceSizeInBytes)
            .WithParameter("$defaultMaxWorkspaceTeamMembers", defaultMaxWorkspaceTeamMembers)
            .Execute();
    }

    private static SQLiteOneRowCommandResult<SsoUser> TrySelectUser(
        Email email,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        return dbWriteContext
            .OneRowCmd(
                sql: """
                     SELECT
                         u_id,
                         u_external_id
                     FROM u_users
                     WHERE u_normalized_email = $userNormalizedEmail
                     LIMIT 1
                     """,
                readRowFunc: reader => new SsoUser
                {
                    Id = reader.GetInt32(0),
                    ExternalId = reader.GetExtId<UserExtId>(1),
                    Email = email
                },
                transaction: transaction)
            .WithParameter("$userNormalizedEmail", email.Normalized)
            .Execute();
    }

    public enum ResultCode
    {
        ExistingUser,
        NewUserCreated,
        RegistrationNotAllowed
    }

    public record Result(
        ResultCode Code,
        SsoUser? User = null);

    public class SsoUser
    {
        public required int Id { get; init; }
        public required UserExtId ExternalId { get; init; }
        public required Email Email { get; init; }
    }
}
