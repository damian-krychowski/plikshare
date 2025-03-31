using Microsoft.Data.Sqlite;
using PlikShare.Core.Authorization;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Emails;
using PlikShare.Core.Emails.Definitions;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Users.Cache;
using PlikShare.Users.Entities;
using PlikShare.Users.Id;
using Serilog;

namespace PlikShare.Users.Invite;

public class InviteUsersQuery(
    DbWriteQueue dbWriteQueue,
    IQueue queue,
    IClock clock,
    IOneTimeInvitationCode oneTimeInvitationCode)
{
    public Task<List<InvitedUser>> Execute(
        List<Email> emails,
        UserContext inviter,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                emails,
                inviter,
                correlationId),
            cancellationToken: cancellationToken);
    }

    private List<InvitedUser> ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        List<Email> emails,
        UserContext inviter,
        Guid correlationId)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var users = new List<InvitedUser>();

            foreach (var email in emails)
            {
                var user = TryInsertUserInvitation(
                    email: email,
                    inviter: inviter,
                    correlationId: correlationId,
                    dbWriteContext: dbWriteContext,
                    transaction: transaction);

                if (!user.IsEmpty)
                {
                    users.Add(user.Value);
                }
            }

            transaction.Commit();

            return users;
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e, "Something went wrong while inviting Users '{UserEmails}'",
                emails.Select(e => e.Anonymize()).ToArray());

            throw;
        }
    }

    private SQLiteOneRowCommandResult<InvitedUser> TryInsertUserInvitation(
        Email email,
        UserContext inviter,
        Guid correlationId,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        var normalizedEmail = email.Normalized;
        var invitationCode = oneTimeInvitationCode.Generate();
        var securityStamp = Guid.NewGuid().ToString();
        var concurrencyStamp = Guid.NewGuid().ToString();

        var user = dbWriteContext
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
                         u_default_max_workspace_size_in_bytes
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
                         NULL,
                         NULL
                     )
                     ON CONFLICT(u_normalized_email) DO NOTHING
                     RETURNING
                         u_id,
                         u_external_id
                     """,
                readRowFunc: reader => new InvitedUser(
                    Id: reader.GetInt32(0),
                    ExternalId: reader.GetExtId<UserExtId>(1),
                    Email: email,
                    InvitationCode: invitationCode),
                transaction: transaction)
            .WithParameter("$externalId", UserExtId.NewId().Value)
            .WithParameter("$userName", email.Value)
            .WithParameter("$normalizedUserName", normalizedEmail)
            .WithParameter("$email", email.Value)
            .WithParameter("$normalizedEmail", normalizedEmail)
            .WithParameter("$securityStamp", securityStamp)
            .WithParameter("$concurrencyStamp", concurrencyStamp)
            .WithParameter("$invitationCode", invitationCode)
            .Execute();

        if (user.IsEmpty)
            return user;

        var addPermissionResult = dbWriteContext
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
            .WithParameter("$userId", user.Value.Id)
            .WithParameter("$claimType", Claims.Permission)
            .WithParameter("$claimValue", Permissions.AddWorkspace)
            .Execute();

        if (addPermissionResult.IsEmpty)
        {
            throw new InvalidOperationException(
                $"Something went wrong while adding '{Permissions.AddWorkspace}' permission to User '{user.Value.Id}'");
        }

        queue.EnqueueOrThrow(
            correlationId: correlationId,
            jobType: EmailQueueJobType.Value,
            definition: new EmailQueueJobDefinition<UserInvitationEmailDefinition>
            {
                Email = email.Value,
                Template = EmailTemplate.UserInvitation,
                Definition = new UserInvitationEmailDefinition(
                    InviterEmail: inviter.Email.Value,
                    InvitationCode: user.Value.InvitationCode)
            },
            executeAfterDate: clock.UtcNow,
            debounceId: null,
            sagaId: null,
            dbWriteContext: dbWriteContext,
            transaction: transaction);

        return user;
    }

    public readonly record struct InvitedUser(
        int Id,
        UserExtId ExternalId,
        Email Email,
        string InvitationCode);
}