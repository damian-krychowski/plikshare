using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.GeneralSettings;
using PlikShare.Users.Cache;
using PlikShare.Users.Entities;
using PlikShare.Users.Id;
using PlikShare.Users.Invite;
using Serilog;

namespace PlikShare.Users.GetOrCreate;

public class GetOrCreateUserInvitationQuery(
    DbWriteQueue dbWriteQueue,
    AppSettings appSettings,
    IOneTimeInvitationCode oneTimeInvitationCode)
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
        SqliteWriteContext dbWriteContext,
        Email email)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var result = ExecuteTransaction(
                dbWriteContext: dbWriteContext,
                transaction: transaction,
                email: email);

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

    /// <summary>
    /// Get-or-create variant for callers composing a larger transaction (e.g. workspace
    /// invite + auto-grant). Runs inside the caller's transaction so user invitation row,
    /// membership insert, and any follow-up writes either commit together or roll back
    /// together — no orphan invitation rows on partial failure.
    /// </summary>
    public User ExecuteTransaction(
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction,
        Email email)
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

        return userResult.Value;
    }

    private SQLiteOneRowCommandResult<User> TryInsertUserInvitation(
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
                readRowFunc: reader => new User(
                    Id: reader.GetInt32(0),
                    ExternalId: externalId,
                    Email: email,
                    EncryptionMetadata: null,
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

    private SQLiteOneRowCommandResult<User> TrySelectUser(
        Email email,
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction)
    {
        return dbWriteContext
            .OneRowCmd(
                sql: """
                      SELECT
                          u_id,
                          u_external_id,
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
                    var encryptionPublicKey = reader.GetFieldValueOrNull<byte[]>(2);

                    return new User(
                        Id: reader.GetInt32(0),
                        ExternalId: reader.GetExtId<UserExtId>(1),
                        Email: email,
                        EncryptionMetadata: encryptionPublicKey is null
                            ? null
                            : new UserEncryptionMetadata
                            {
                                PublicKey = encryptionPublicKey,
                                EncryptedPrivateKey = reader.GetFieldValue<byte[]>(3),
                                KdfSalt = reader.GetFieldValue<byte[]>(4),
                                KdfParams = EncryptionPasswordKdf.DeserializeParams(reader.GetString(5)),
                                VerifyHash = reader.GetFieldValue<byte[]>(6),
                                RecoveryWrappedPrivateKey = reader.GetFieldValue<byte[]>(7),
                                RecoveryVerifyHash = reader.GetFieldValue<byte[]>(8)
                            },
                        InvitationCode: null);
                },
                transaction: transaction)
            .WithParameter("$userNormalizedEmail", email.Normalized)
            .Execute();
    }

    public record User(
        int Id,
        UserExtId ExternalId,
        Email Email,
        UserEncryptionMetadata? EncryptionMetadata,
        InvitationCode? InvitationCode);
}