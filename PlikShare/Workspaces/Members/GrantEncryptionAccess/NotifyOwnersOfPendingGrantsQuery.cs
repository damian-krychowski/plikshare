using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.Workspaces.Members.GrantEncryptionAccess;

/// <summary>
/// Enqueues one "owner must grant encryption access" email for every full-encrypted
/// workspace the user is a member of but does not yet have a wek wrap in. Intended to run
/// right after the user finishes their encryption password setup — until then the owner
/// had nothing to act on, so the email was deferred.
/// </summary>
public class NotifyOwnersOfPendingGrantsQuery(
    DbWriteQueue dbWriteQueue,
    IQueue queue,
    IClock clock)
{
    public Task<int> Execute(
        int userId,
        string inviteeEmail,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context =>
            {
                using var transaction = context.Connection.BeginTransaction();

                try
                {
                    var rows = context
                        .Cmd(
                            sql: """
                                 SELECT
                                     w.w_name,
                                     owner.u_email
                                 FROM wm_workspace_membership wm
                                 INNER JOIN w_workspaces w ON w.w_id = wm.wm_workspace_id
                                 INNER JOIN s_storages s ON s.s_id = w.w_storage_id
                                 INNER JOIN u_users owner ON owner.u_id = w.w_owner_id
                                 WHERE wm.wm_member_id = $userId
                                   AND s.s_encryption_type = 'full'
                                   AND NOT EXISTS (
                                       SELECT 1 FROM wek_workspace_encryption_keys wek
                                       WHERE wek.wek_workspace_id = wm.wm_workspace_id
                                         AND wek.wek_user_id = wm.wm_member_id
                                   )
                                 """,
                            readRowFunc: reader => new PendingGrantRow(
                                WorkspaceName: reader.GetString(0),
                                OwnerEmail: reader.GetString(1)),
                            transaction: transaction)
                        .WithParameter("$userId", userId)
                        .Execute();

                    foreach (var row in rows)
                    {
                        OwnerGrantRequiredEmail.Enqueue(
                            queue: queue,
                            clock: clock,
                            correlationId: correlationId,
                            inviteeEmail: inviteeEmail,
                            ownerEmail: row.OwnerEmail,
                            workspaceName: row.WorkspaceName,
                            dbWriteContext: context,
                            transaction: transaction);
                    }

                    transaction.Commit();

                    if (rows.Count > 0)
                    {
                        Log.Information(
                            "User#{UserId} finished encryption setup — notified {OwnerCount} workspace owner(s) of pending key grants.",
                            userId, rows.Count);
                    }

                    return rows.Count;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            },
            cancellationToken: cancellationToken);
    }

    private readonly record struct PendingGrantRow(
        string WorkspaceName,
        string OwnerEmail);
}
