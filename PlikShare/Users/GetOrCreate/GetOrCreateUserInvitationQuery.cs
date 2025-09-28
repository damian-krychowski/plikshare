using Microsoft.Data.Sqlite;
using PlikShare.Core.Authorization;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.GeneralSettings;
using PlikShare.Users.Entities;
using PlikShare.Users.Id;
using PlikShare.Users.Sql;
using Serilog;

namespace PlikShare.Users.GetOrCreate;

public class GetOrCreateUserInvitationQuery(
    DbWriteQueue dbWriteQueue,
    AppSettings appSettings)
{
    public Task<User> Execute(
        Email email,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                email),
            cancellationToken: cancellationToken);
    }

    private User ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        Email email)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var user = GetOrCreateUserInvitation(
                email: email,
                dbWriteContext: dbWriteContext,
                transaction: transaction);

            transaction.Commit();

            if (user.WasJustCreated)
            {
                Log.Information("User '{User}' was created.",
                    user);
            }

            return user;
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e, "Something went wrong while creating User '{UserEmail}'",
                email.Anonymize());

            throw;
        }
    }

    private User GetOrCreateUserInvitation(
        Email email,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        var userResult = TrySelectUser(
            email,
            dbWriteContext,
            transaction);

        if (!userResult.IsEmpty)
            return userResult.Value;

        var newUserResult = TryInsertUserInvitation(
            email,
            dbWriteContext,
            transaction);

        //user was just created, we can return it
        if (!newUserResult.IsEmpty)
            return newUserResult.Value;

        //user was not created - which means it was created concurrently in the meantime
        //we try to select it again
        userResult = TrySelectUser(
            email,
            dbWriteContext,
            transaction);

        if (userResult.IsEmpty)
        {
            //that is an impossible scenario
            throw new InvalidOperationException($"Cannot create nor select user with email '{email}'");
        }

        return userResult.Value;
    }

    private SQLiteOneRowCommandResult<User> TryInsertUserInvitation(
        Email email,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        var externalId = UserExtId.NewId();
        var normalizedEmail = email.Normalized;
        var invitationCode = Guid.NewGuid().ToBase62();
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
                         FALSE,
                         NULL,
                         $securityStamp,
                         $concurrencyStamp,
                         NULL,
                         FALSE,
                         FALSE,
                         NULL,
                         FALSE,
                         0,
                         TRUE,
                         $invitationCode,
                         $maxWorkspaceNumber,
                         $defaultMaxWorkspaceSizeInBytes,
                         $defaultMaxWorkspaceTeamMembers
                     )
                     ON CONFLICT(u_normalized_email) DO NOTHING
                     RETURNING
                         u_id
                     """,
                readRowFunc: reader => new User
                {
                    Id = reader.GetInt32(0),
                    ExternalId = externalId,
                    IsEmailConfirmed = false,
                    IsInvitation = true,
                    InvitationCode = invitationCode,
                    SecurityStamp = securityStamp,
                    ConcurrencyStamp = concurrencyStamp,
                    IsAdmin = false,
                    CanAddWorkspace = false,
                    CanManageGeneralSettings = false,
                    CanManageUsers = false,
                    CanManageStorages = false,
                    CanManageEmailProviders = false,
                    CanManageAuth = false,
                    CanManageIntegrations = false,
                    MaxWorkspaceNumber = maxWorkspaceNumber,
                    DefaultMaxWorkspaceSizeInBytes = defaultMaxWorkspaceSizeInBytes,
                    DefaultMaxWorkspaceTeamMembers = defaultMaxWorkspaceTeamMembers,
                    WasJustCreated = true
                },
                transaction: transaction)
            .WithParameter("$externalId", externalId.Value)
            .WithParameter("$userName", email.Value)
            .WithParameter("$normalizedUserName", normalizedEmail)
            .WithParameter("$email", email.Value)
            .WithParameter("$normalizedEmail", normalizedEmail)
            .WithParameter("$securityStamp", securityStamp)
            .WithParameter("$concurrencyStamp", concurrencyStamp)
            .WithParameter("$invitationCode", invitationCode)
            .WithParameter("$maxWorkspaceNumber", maxWorkspaceNumber)
            .WithParameter("$defaultMaxWorkspaceSizeInBytes", defaultMaxWorkspaceSizeInBytes)
            .WithParameter("$defaultMaxWorkspaceTeamMembers", defaultMaxWorkspaceTeamMembers)
            .Execute();
    }

    private static SQLiteOneRowCommandResult<User> TrySelectUser(
        Email email,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        return dbWriteContext
            .OneRowCmd(
                sql: $"""
                      SELECT
                          u_id,
                          u_external_id,
                          u_email_confirmed,
                          u_is_invitation,
                          u_invitation_code,
                          u_security_stamp,
                          u_concurrency_stamp,
                          ({UserSql.HasRole(Roles.Admin)}) AS u_is_admin,
                          ({UserSql.HasClaim(Claims.Permission, Permissions.AddWorkspace)}) AS u_can_add_workspace,
                          ({UserSql.HasClaim(Claims.Permission, Permissions.ManageGeneralSettings)}) AS u_can_manage_general_settings,
                          ({UserSql.HasClaim(Claims.Permission, Permissions.ManageUsers)}) AS u_can_manage_users,
                          ({UserSql.HasClaim(Claims.Permission, Permissions.ManageStorages)}) AS u_can_manage_storages,
                          ({UserSql.HasClaim(Claims.Permission, Permissions.ManageEmailProviders)}) AS u_can_manage_email_providers,
                          ({UserSql.HasClaim(Claims.Permission, Permissions.ManageAuth)}) AS u_can_manage_auth,
                          ({UserSql.HasClaim(Claims.Permission, Permissions.ManageIntegrations)}) AS u_can_manage_integrations,
                          u_max_workspace_number,
                          u_default_max_workspace_size_in_bytes,
                          u_default_max_workspace_team_members
                      FROM u_users
                      WHERE u_normalized_email = $userNormalizedEmail
                      LIMIT 1
                      """,
                readRowFunc: reader => new User
                {
                    Id = reader.GetInt32(0),
                    ExternalId = reader.GetExtId<UserExtId>(1),
                    IsEmailConfirmed = reader.GetBoolean(2),
                    IsInvitation = reader.GetBoolean(3),
                    InvitationCode = reader.GetStringOrNull(4),
                    SecurityStamp = reader.GetString(5),
                    ConcurrencyStamp = reader.GetString(6),
                    IsAdmin = reader.GetBoolean(7),
                    CanAddWorkspace = reader.GetBoolean(8),
                    CanManageGeneralSettings = reader.GetBoolean(9),
                    CanManageUsers = reader.GetBoolean(10),
                    CanManageStorages = reader.GetBoolean(11),
                    CanManageEmailProviders = reader.GetBoolean(12),
                    CanManageAuth = reader.GetBoolean(13),
                    CanManageIntegrations = reader.GetBoolean(14),
                    MaxWorkspaceNumber = reader.GetInt32OrNull(15),
                    DefaultMaxWorkspaceSizeInBytes = reader.GetInt64OrNull(16),
                    DefaultMaxWorkspaceTeamMembers = reader.GetInt32OrNull(17),
                    WasJustCreated = false
                },
                transaction: transaction)
            .WithParameter("$userNormalizedEmail", email.Normalized)
            .Execute();
    }

    public class User
    {
        public required int Id { get; init; }
        public required UserExtId ExternalId { get; init; }
        public required bool IsEmailConfirmed { get; init; }
        public required bool IsInvitation { get; init; }
        public required string? InvitationCode { get; init; }
        public required string SecurityStamp { get; init; }
        public required string ConcurrencyStamp { get; init; }
        public required bool IsAdmin { get; init; }
        public required bool CanAddWorkspace { get; init; }
        public required bool CanManageGeneralSettings { get; init; }
        public required bool CanManageUsers { get; init; }
        public required bool CanManageStorages { get; init; }
        public required bool CanManageEmailProviders { get; init; }
        public required bool CanManageAuth { get; init; }
        public required bool CanManageIntegrations { get; init; }
        public required int? MaxWorkspaceNumber { get; init; }
        public required long? DefaultMaxWorkspaceSizeInBytes { get; init; }
        public required int? DefaultMaxWorkspaceTeamMembers { get; init; }
        public required bool WasJustCreated { get; init; }
    }
}