using Microsoft.Data.Sqlite;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Emails;
using PlikShare.Core.Emails.Definitions;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;

namespace PlikShare.Workspaces.Members.GrantEncryptionAccess;

/// <summary>
/// Shared enqueue for the "owner must grant encryption access" email. Called both from
/// workspace invitation (when the invitee already has encryption set up and can be granted
/// immediately) and from user encryption password setup (for each full-encrypted workspace
/// the freshly-set-up user is already a member of).
/// </summary>
public static class OwnerGrantRequiredEmail
{
    public static SQLiteOneRowCommandResult<QueueJobId> Enqueue(
        IQueue queue,
        IClock clock,
        Guid correlationId,
        string inviteeEmail,
        string ownerEmail,
        string workspaceName,
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction)
    {
        return queue.Enqueue(
            correlationId: correlationId,
            jobType: EmailQueueJobType.Value,
            definition: new EmailQueueJobDefinition<WorkspaceEncryptionKeyGrantRequiredEmailDefinition>
            {
                Email = ownerEmail,
                Template = EmailTemplate.WorkspaceEncryptionKeyGrantRequired,
                Definition = new WorkspaceEncryptionKeyGrantRequiredEmailDefinition(
                    InviteeEmail: inviteeEmail,
                    WorkspaceName: workspaceName)
            },
            executeAfterDate: clock.UtcNow,
            debounceId: null,
            sagaId: null,
            dbWriteContext: dbWriteContext,
            transaction: transaction);
    }
}
