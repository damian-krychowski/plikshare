using Microsoft.Data.Sqlite;
using PlikShare.Core.Authorization;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.GeneralSettings;
using PlikShare.Users.Cache;
using PlikShare.Users.Entities;
using PlikShare.Users.Id;
using PlikShare.Users.Invite;
using PlikShare.Users.Sql;
using Serilog;

namespace PlikShare.Users.GetOrCreate;

public class GetOrCreateUserInvitationQuery(
    DbWriteQueue dbWriteQueue,
    AppOwners appOwners,
    AppSettings appSettings,
    IOneTimeInvitationCode oneTimeInvitationCode)
{
    public Task<Result> Execute(
        Email email,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                email),
            cancellationToken: cancellationToken);
    }

    private Result ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        Email email)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var result = GetOrCreateUserInvitation(
                email: email,
                dbWriteContext: dbWriteContext,
                transaction: transaction);

            transaction.Commit();

            if (result.InvitationCode is not null)
            {
                Log.Information("User '{UserEmail}' was created.",
                    email.Anonymize());
            }

            return result;
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e, "Something went wrong while creating User '{UserEmail}'",
                email.Anonymize());

            throw;
        }
    }

    private Result GetOrCreateUserInvitation(
        Email email,
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction)
    {
        var userResult = TrySelectUser(
            email,
            dbWriteContext,
            transaction);

        if (!userResult.IsEmpty)
            return new Result(
                User: userResult.Value,
                InvitationCode: null);

        var newUserResult = TryInsertUserInvitation(
            email,
            dbWriteContext,
            transaction);

        if (!newUserResult.IsEmpty)
            return newUserResult.Value;

        userResult = TrySelectUser(
            email,
            dbWriteContext,
            transaction);

        if (userResult.IsEmpty)
        {
            throw new InvalidOperationException(
                $"Cannot create nor select user with email '{email}'");
        }

        return new Result(
            User: userResult.Value,
            InvitationCode: null);
    }

    private SQLiteOneRowCommandResult<Result> TryInsertUserInvitation(
        Email email,
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction)
    {
        var externalId = UserExtId.NewId();
        var normalizedEmail = email.Normalized;
        var invitationCode = oneTimeInvitationCode.Generate();
        var invitationCodeHash = InvitationCodeHasher.Hash(invitationCode);
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
                         u_invitation_code_hash,
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
                         $invitationCodeHash,
                         $maxWorkspaceNumber,
                         $defaultMaxWorkspaceSizeInBytes,
                         $defaultMaxWorkspaceTeamMembers
                     )
                     ON CONFLICT(u_normalized_email) DO NOTHING
                     RETURNING
                         u_id
                     """,
                readRowFunc: reader => new Result(
                    User: new UserContext
                    {
                        Status = UserStatus.Invitation,
                        Id = reader.GetInt32(0),
                        ExternalId = externalId,
                        Email = email,
                        IsEmailConfirmed = false,
                        Stamps = new UserSecurityStamps
                        {
                            Security = securityStamp,
                            Concurrency = concurrencyStamp
                        },
                        Roles = new UserRoles
                        {
                            IsAppOwner = appOwners.IsAppOwner(email),
                            IsAdmin = false
                        },
                        Permissions = new UserPermissions
                        {
                            CanAddWorkspace = false,
                            CanManageGeneralSettings = false,
                            CanManageUsers = false,
                            CanManageStorages = false,
                            CanManageEmailProviders = false,
                            CanManageAuth = false,
                            CanManageIntegrations = false,
                            CanManageAuditLog = false
                        },
                        Invitation = new UserInvitation { CodeHash = invitationCodeHash },
                        MaxWorkspaceNumber = maxWorkspaceNumber,
                        DefaultMaxWorkspaceSizeInBytes = defaultMaxWorkspaceSizeInBytes,
                        DefaultMaxWorkspaceTeamMembers = defaultMaxWorkspaceTeamMembers,
                        HasPassword = false,
                        EncryptionMetadata = null
                    },
                    InvitationCode: new InvitationCode
                    {
                        Value = invitationCode
                    }),
                transaction: transaction)
            .WithParameter("$externalId", externalId.Value)
            .WithParameter("$userName", email.Value)
            .WithParameter("$normalizedUserName", normalizedEmail)
            .WithParameter("$email", email.Value)
            .WithParameter("$normalizedEmail", normalizedEmail)
            .WithParameter("$securityStamp", securityStamp)
            .WithParameter("$concurrencyStamp", concurrencyStamp)
            .WithParameter("$invitationCodeHash", invitationCodeHash)
            .WithParameter("$maxWorkspaceNumber", maxWorkspaceNumber)
            .WithParameter("$defaultMaxWorkspaceSizeInBytes", defaultMaxWorkspaceSizeInBytes)
            .WithParameter("$defaultMaxWorkspaceTeamMembers", defaultMaxWorkspaceTeamMembers)
            .Execute();
    }

    private SQLiteOneRowCommandResult<UserContext> TrySelectUser(
        Email email,
        SqliteWriteContext dbWriteContext,
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
                          u_invitation_code_hash,
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
                          ({UserSql.HasClaim(Claims.Permission, Permissions.ManageAuditLog)}) AS u_can_manage_audit_log,
                          u_max_workspace_number,
                          u_default_max_workspace_size_in_bytes,
                          u_default_max_workspace_team_members,
                          (u_password_hash IS NOT NULL) AS u_has_password,
                          u_encryption_public_key,
                          u_encryption_encrypted_private_key,
                          u_encryption_kdf_salt,
                          u_encryption_kdf_params,
                          u_encryption_verify_hash,
                          u_encryption_recovery_wrapped_private_key,
                          u_encryption_recovery_verify_hash
                      FROM u_users
                      WHERE u_normalized_email = $userNormalizedEmail
                      LIMIT 1
                      """,
                readRowFunc: reader =>
                {
                    var isInvitation = reader.GetBoolean(3);
                    var encryptionPublicKey = reader.GetFieldValueOrNull<byte[]>(20);

                    return new UserContext
                    {
                        Status = isInvitation
                            ? UserStatus.Invitation
                            : UserStatus.Registered,
                        Id = reader.GetInt32(0),
                        ExternalId = reader.GetExtId<UserExtId>(1),
                        Email = email,
                        IsEmailConfirmed = reader.GetBoolean(2),

                        Stamps = new UserSecurityStamps
                        {
                            Security = reader.GetString(5),
                            Concurrency = reader.GetString(6)
                        },

                        Roles = new UserRoles
                        {
                            IsAppOwner = appOwners.IsAppOwner(email),
                            IsAdmin = reader.GetBoolean(7)
                        },

                        Permissions = new UserPermissions
                        {
                            CanAddWorkspace = reader.GetBoolean(8),
                            CanManageGeneralSettings = reader.GetBoolean(9),
                            CanManageUsers = reader.GetBoolean(10),
                            CanManageStorages = reader.GetBoolean(11),
                            CanManageEmailProviders = reader.GetBoolean(12),
                            CanManageAuth = reader.GetBoolean(13),
                            CanManageIntegrations = reader.GetBoolean(14),
                            CanManageAuditLog = reader.GetBoolean(15)
                        },

                        Invitation = isInvitation
                            ? new UserInvitation { CodeHash = reader.GetFieldValue<byte[]>(4) }
                            : null,

                        MaxWorkspaceNumber = reader.GetInt32OrNull(16),
                        DefaultMaxWorkspaceSizeInBytes = reader.GetInt64OrNull(17),
                        DefaultMaxWorkspaceTeamMembers = reader.GetInt32OrNull(18),
                        HasPassword = reader.GetBoolean(19),

                        EncryptionMetadata = encryptionPublicKey is null
                            ? null
                            : new UserEncryptionMetadata
                            {
                                PublicKey = encryptionPublicKey,
                                EncryptedPrivateKey = reader.GetFieldValue<byte[]>(21),
                                KdfSalt = reader.GetFieldValue<byte[]>(22),
                                KdfParams = EncryptionPasswordKdf.DeserializeParams(reader.GetString(23)),
                                VerifyHash = reader.GetFieldValue<byte[]>(24),
                                RecoveryWrappedPrivateKey = reader.GetFieldValue<byte[]>(25),
                                RecoveryVerifyHash = reader.GetFieldValue<byte[]>(26)
                            }
                    };
                },
                transaction: transaction)
            .WithParameter("$userNormalizedEmail", email.Normalized)
            .Execute();
    }

    public record Result(
        UserContext User,
        InvitationCode? InvitationCode = null);
}