using Flurl;
using Microsoft.Data.Sqlite;
using PlikShare.Core.Clock;
using PlikShare.Core.Configuration;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Emails;
using PlikShare.Core.Emails.Definitions;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.GeneralSettings;
using PlikShare.Users.Cache;
using PlikShare.Users.Entities;
using PlikShare.Users.Id;
using PlikShare.Users.Invite.Contracts;
using PlikShare.Users.PermissionsAndRoles;
using PlikShare.Users.StorageAccess;
using PlikShare.Users.UpdatePermissionsAndRoles;
using Serilog;

namespace PlikShare.Users.Invite;

public class InviteUsersQuery(
    DbWriteQueue dbWriteQueue,
    IQueue queue,
    IClock clock,
    IOneTimeInvitationCode oneTimeInvitationCode,
    AppSettings appSettings,
    IConfig config)
{
    public Task<InviteUsersResponseDto> Execute(
        List<Email> emails,
        InvitationDeliveryMethod deliveryMethod,
        UserContext inviter,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                emails,
                deliveryMethod,
                inviter,
                correlationId),
            cancellationToken: cancellationToken);
    }

    private InviteUsersResponseDto ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        List<Email> emails,
        InvitationDeliveryMethod deliveryMethod,
        UserContext inviter,
        Guid correlationId)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var storageAccessSetting = appSettings.NewUserDefaultStorageAccess;

            var snapshottedStorageIds = storageAccessSetting.Mode == UserStorageAccessMode.All
                ? []
                : ResolveStorageIds(
                    externalIds: storageAccessSetting.StorageExternalIds,
                    dbWriteContext: dbWriteContext,
                    transaction: transaction);

            var users = new List<InvitedUserDto>();

            foreach (var email in emails)
            {
                var user = TryInsertUserInvitation(
                    email: email,
                    deliveryMethod: deliveryMethod,
                    inviter: inviter,
                    correlationId: correlationId,
                    storageAccessMode: storageAccessSetting.Mode,
                    storageAccessIds: snapshottedStorageIds,
                    dbWriteContext: dbWriteContext,
                    transaction: transaction);

                if (user is not null)
                {
                    users.Add(user);
                }
            }

            transaction.Commit();

            return new InviteUsersResponseDto
            {
                Users = users
            };
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e, "Something went wrong while inviting Users '{UserEmails}'",
                emails.Select(e => e.Anonymize()).ToArray());

            throw;
        }
    }

    private static List<int> ResolveStorageIds(
        IReadOnlyList<string> externalIds,
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction)
    {
        var resolved = new List<int>(externalIds.Count);

        foreach (var externalId in externalIds)
        {
            var result = dbWriteContext
                .OneRowCmd(
                    sql: """
                         SELECT s_id
                         FROM s_storages
                         WHERE s_external_id = $externalId
                         """,
                    readRowFunc: reader => reader.GetInt32(0),
                    transaction: transaction)
                .WithParameter("$externalId", externalId)
                .Execute();

            if (result.IsEmpty)
            {
                Log.Warning(
                    "Storage with external id '{ExternalId}' was referenced in default storage access setting but no longer exists; skipping for new invitations.",
                    externalId);
                continue;
            }

            resolved.Add(result.Value);
        }

        return resolved;
    }

    private InvitedUserDto? TryInsertUserInvitation(
        Email email,
        InvitationDeliveryMethod deliveryMethod,
        UserContext inviter,
        Guid correlationId,
        UserStorageAccessMode storageAccessMode,
        List<int> storageAccessIds,
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction)
    {
        var normalizedEmail = email.Normalized;
        var invitationCode = oneTimeInvitationCode.Generate();
        var invitationCodeHash = InvitationCodeHasher.Hash(invitationCode);
        var securityStamp = Guid.NewGuid().ToString();
        var concurrencyStamp = Guid.NewGuid().ToString();
        var externalId = UserExtId.NewId();

        var maxWorkspaceNumber = appSettings.NewUserDefaultMaxWorkspaceNumber.Value;
        var defaultMaxWorkspaceSizeInBytes = appSettings.NewUserDefaultMaxWorkspaceSizeInBytes.Value;
        var defaultMaxWorkspaceTeamMembers = appSettings.NewUserDefaultMaxWorkspaceTeamMembers.Value;

        var userId = dbWriteContext
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
                         u_default_max_workspace_team_members,
                         u_storage_access_mode
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
                         $defaultMaxWorkspaceTeamMembers,
                         $storageAccessMode
                     )
                     ON CONFLICT(u_normalized_email) DO NOTHING
                     RETURNING
                         u_id
                     """,
                readRowFunc: reader => reader.GetInt32(0),
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
            .WithEnumParameter("$storageAccessMode", storageAccessMode)
            .Execute();

        if (userId.IsEmpty)
            return null;

        foreach (var storageId in storageAccessIds)
        {
            dbWriteContext
                .OneRowCmd(
                    sql: """
                         INSERT INTO usa_user_storage_access (
                             usa_user_id,
                             usa_storage_id
                         ) VALUES (
                             $userId,
                             $storageId
                         )
                         ON CONFLICT (usa_user_id, usa_storage_id) DO NOTHING
                         RETURNING usa_user_id
                         """,
                    readRowFunc: reader => reader.GetInt32(0),
                    transaction: transaction)
                .WithParameter("$userId", userId.Value)
                .WithParameter("$storageId", storageId)
                .Execute();
        }

        var permissionsAndRoles = appSettings
            .NewUserDefaultPermissionsAndRoles;

        if (permissionsAndRoles.IsAdmin)
        {
            UpdateUserPermissionsAndRoleQuery.AddAdminRole(
                userId: userId.Value,
                adminRoleId: appSettings.AdminRoleId,
                dbWriteContext: dbWriteContext,
                transaction: transaction);
        }

        var defaultPermissions = permissionsAndRoles
            .GetPermissions();

        if (defaultPermissions.Any())
        {
            UpdateUserPermissionsAndRoleQuery.AddPermissions(
                userId: userId.Value,
                isAdmin: permissionsAndRoles.IsAdmin,
                permissions: defaultPermissions,
                dbWriteContext: dbWriteContext,
                transaction: transaction);
        }

        string? invitationLink = null;

        if (deliveryMethod == InvitationDeliveryMethod.Email)
        {
            queue.EnqueueOrThrow(
                correlationId: correlationId,
                jobType: EmailQueueJobType.Value,
                definition: new EmailQueueJobDefinition<UserInvitationEmailDefinition>
                {
                    Email = email.Value,
                    Template = EmailTemplate.UserInvitation,
                    Definition = new UserInvitationEmailDefinition(
                        InviterEmail: inviter.Email.Value,
                        InvitationCode: invitationCode)
                },
                executeAfterDate: clock.UtcNow,
                debounceId: null,
                sagaId: null,
                batch: null,
                dbWriteContext: dbWriteContext,
                transaction: transaction);
        }
        else
        {
            invitationLink = new Url(config.AppUrl)
                .AppendPathSegment("sign-up")
                .AppendQueryParam("invitationCode", invitationCode)
                .ToString();
        }

        return new InvitedUserDto
        {
            ExternalId = externalId,
            Email = email.Value,

            MaxWorkspaceNumber = maxWorkspaceNumber,
            DefaultMaxWorkspaceSizeInBytes = defaultMaxWorkspaceSizeInBytes,
            DefaultMaxWorkspaceTeamMembers = defaultMaxWorkspaceTeamMembers,
            PermissionsAndRoles = new UserPermissionsAndRolesDto
            {
                IsAdmin = permissionsAndRoles.IsAdmin,

                CanAddWorkspace = permissionsAndRoles.CanAddWorkspace,
                CanManageEmailProviders = permissionsAndRoles.CanManageEmailProviders,
                CanManageGeneralSettings = permissionsAndRoles.CanManageGeneralSettings,
                CanManageStorages = permissionsAndRoles.CanManageStorages,
                CanManageUsers = permissionsAndRoles.CanManageUsers,
                CanManageAuth = permissionsAndRoles.CanManageAuth,
                CanManageIntegrations = permissionsAndRoles.CanManageIntegrations,
                CanManageAuditLog = permissionsAndRoles.CanManageAuditLog
            },
            InvitationLink = invitationLink
        };
    }
}