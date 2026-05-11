using Microsoft.Data.Sqlite;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Storages.Encryption;
using PlikShare.Users.Cache;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Encryption;
using PlikShare.Workspaces.Members.GrantEncryptionAccess;
using Serilog;

namespace PlikShare.Workspaces.Members.AdminAdd;

/// <summary>
/// Direct assignment of a target user to a workspace by an admin holding the
/// <c>ManageUsers</c> permission. Unlike the owner-driven invitation path
/// (<see cref="CreateInvitation.CreateWorkspaceMemberInvitationOperation"/>), this is NOT an
/// invitation: the membership row is inserted with <c>wm_was_invitation_accepted = TRUE</c>
/// and NO invitation email is sent. The target user becomes a full member immediately.
///
/// Encryption handling (full-encryption storages only) mirrors
/// <see cref="ChangeOwner.ChangeWorkspaceOwnerQuery"/>: the admin's DPAPI-protected private
/// key is read from the encryption session cookie and used to unseal a Workspace DEK via
/// either their per-user <c>wek</c> row or storage-admin <c>sek</c> derivation. The unsealed
/// DEK is re-wrapped to the target's public key inside the same transaction as the
/// membership insert.
///
/// Targets with encryption already configured get an immediate wek wrap (auto-grant).
/// Targets without encryption get the membership row only; when they later set up encryption,
/// <see cref="NotifyOwnersOfPendingGrantsQuery"/> notifies the workspace owner so they can
/// grant access manually. Invitation-only users (never registered) are rejected — direct
/// assignment requires a real account.
/// </summary>
public class AdminAddWorkspaceMemberOperation(
    DbWriteQueue dbWriteQueue,
    IClock clock,
    UpsertWorkspaceEncryptionKeyQuery upsertWorkspaceEncryptionKeyQuery)
{
    public async Task<ResultCode> Execute(
        WorkspaceContext workspace,
        UserContext actor,
        UserContext target,
        bool allowShare,
        SecureBytes? actorPrivateKey,
        CancellationToken cancellationToken)
    {
        if (target.Status != UserStatus.Registered)
            return ResultCode.TargetNotRegistered;

        if (workspace.Storage.Encryption is not FullStorageEncryption)
        {
            return await dbWriteQueue.Execute(
                operationToEnqueue: context => ExecuteOperation(
                    dbWriteContext: context,
                    workspace: workspace,
                    actor: actor,
                    target: target,
                    allowShare: allowShare,
                    autoGrantWrapped: []),
                cancellationToken: cancellationToken);
        }

        if (actorPrivateKey is null)
            return ResultCode.ActorEncryptionSessionRequired;

        if (workspace.EncryptionMetadata is null)
            throw new InvalidOperationException(
                $"Workspace#{workspace.Id} is on a full-encryption storage but has no EncryptionMetadata.");

        WorkspaceDekEntry[] entries;
        try
        {
            entries = actor.UnsealWorkspaceDeks(
                workspace: workspace,
                privateKey: actorPrivateKey);
        }
        catch (WorkspaceDekUnsealException e)
        {
            Log.Error(e,
                "AdminAddWorkspaceMember: failed to unseal workspace DEK v{Version} for actor User#{ActorId} on Workspace#{WorkspaceId}.",
                e.StorageDekVersion, actor.Id, e.WorkspaceId);
            return ResultCode.ActorCannotDecryptWorkspace;
        }
        catch (StorageDekUnsealException e)
        {
            Log.Error(e,
                "AdminAddWorkspaceMember: failed to unseal storage DEK v{Version} for actor User#{ActorId} on Storage#{StorageId}.",
                e.StorageDekVersion, actor.Id, e.StorageId);
            return ResultCode.ActorCannotDecryptWorkspace;
        }

        if (entries.Length == 0)
            return ResultCode.ActorCannotDecryptWorkspace;

        try
        {
            // Target without encryption falls into the deferred-grant path: insert
            // membership only; no wek can be wrapped without a public key. Owner will
            // be notified by NotifyOwnersOfPendingGrantsQuery when the target sets up
            // their encryption password.
            GrantEncryptionAccessOperation.WrappedVersion[] autoGrantWrapped;

            if (target.EncryptionMetadata is null)
            {
                autoGrantWrapped = [];
            }
            else
            {
                autoGrantWrapped = entries
                    .Select(entry => new GrantEncryptionAccessOperation.WrappedVersion(
                        StorageDekVersion: entry.StorageDekVersion,
                        WrappedDek: entry.Dek.Use(
                            state: target.EncryptionMetadata.PublicKey,
                            (dekSpan, pubKey) => UserKeyPair.SealTo(pubKey, dekSpan))))
                    .ToArray();
            }

            return await dbWriteQueue.Execute(
                operationToEnqueue: context => ExecuteOperation(
                    dbWriteContext: context,
                    workspace: workspace,
                    actor: actor,
                    target: target,
                    allowShare: allowShare,
                    autoGrantWrapped: autoGrantWrapped),
                cancellationToken: cancellationToken);
        }
        finally
        {
            foreach (var entry in entries)
                entry.Dispose();
        }
    }

    private ResultCode ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        WorkspaceContext workspace,
        UserContext actor,
        UserContext target,
        bool allowShare,
        GrantEncryptionAccessOperation.WrappedVersion[] autoGrantWrapped)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var insertResult = InsertAcceptedMembership(
                dbWriteContext: dbWriteContext,
                transaction: transaction,
                workspaceId: workspace.Id,
                memberId: target.Id,
                inviterId: actor.Id,
                allowShare: allowShare);

            if (insertResult.IsEmpty)
            {
                transaction.Rollback();
                return ResultCode.AlreadyMember;
            }

            foreach (var grant in autoGrantWrapped)
            {
                upsertWorkspaceEncryptionKeyQuery.ExecuteTransaction(
                    dbWriteContext: dbWriteContext,
                    workspaceId: workspace.Id,
                    userId: target.Id,
                    storageDekVersion: grant.StorageDekVersion,
                    wrappedWorkspaceDek: grant.WrappedDek,
                    wrappedByUserId: actor.Id,
                    transaction: transaction);
            }

            transaction.Commit();

            Log.Information(
                "Workspace#{WorkspaceId}: admin User#{ActorId} assigned User#{TargetId} as accepted member " +
                "(allowShare={AllowShare}, wek versions wrapped: {AutoGrantedCount}).",
                workspace.Id, actor.Id, target.Id, allowShare, autoGrantWrapped.Length);

            return ResultCode.Ok;
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e,
                "Failed to admin-assign User#{TargetId} to Workspace#{WorkspaceId} by Actor#{ActorId}.",
                target.Id, workspace.Id, actor.Id);

            throw;
        }
    }

    private SQLiteOneRowCommandResult<int> InsertAcceptedMembership(
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction,
        int workspaceId,
        int memberId,
        int inviterId,
        bool allowShare)
    {
        return dbWriteContext
            .OneRowCmd(
                sql: """
                     INSERT INTO wm_workspace_membership (
                         wm_workspace_id,
                         wm_member_id,
                         wm_inviter_id,
                         wm_was_invitation_accepted,
                         wm_allow_share,
                         wm_created_at
                     ) VALUES (
                         $workspaceId,
                         $memberId,
                         $inviterId,
                         TRUE,
                         $allowShare,
                         $now
                     )
                     ON CONFLICT(wm_workspace_id, wm_member_id) DO NOTHING
                     RETURNING wm_member_id
                     """,
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$workspaceId", workspaceId)
            .WithParameter("$memberId", memberId)
            .WithParameter("$inviterId", inviterId)
            .WithParameter("$allowShare", allowShare)
            .WithParameter("$now", clock.UtcNow)
            .Execute();
    }

    public enum ResultCode
    {
        Ok = 0,
        TargetNotRegistered,
        AlreadyMember,
        ActorEncryptionSessionRequired,
        ActorCannotDecryptWorkspace
    }
}
