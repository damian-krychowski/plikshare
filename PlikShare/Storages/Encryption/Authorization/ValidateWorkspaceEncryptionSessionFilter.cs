using System.Security.Cryptography;
using PlikShare.Core.Authorization;
using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Users.Middleware;
using PlikShare.Workspaces.Encryption;
using PlikShare.Workspaces.Validation;
using Serilog;

namespace PlikShare.Storages.Encryption.Authorization;

/// <summary>
/// Ensures the caller has an unwrapped Workspace DEK available in <see cref="HttpContext.Items"/>
/// for the target workspace when its storage uses full encryption. For None/Managed storages
/// the filter is a no-op.
///
/// The unlock is two-phase:
/// 1. The caller must hold a valid <see cref="UserEncryptionSessionCookie"/> carrying their
///    DPAPI-protected X25519 private key. If not, we short-circuit with 423
///    <c>user-encryption-session-required</c> so the client can prompt for the encryption
///    password.
/// 2. The caller must own a per-user wrap of this workspace's DEK in
///    <c>wek_workspace_encryption_keys</c>. Absence means the user is authenticated and
///    unlocked but is not a member with encrypted-file access to this workspace — a 403
///    <c>not-a-storage-admin</c>.
///
/// On success the filter unseals the wrapped DEK with <see cref="UserKeyPair.OpenSealed"/>
/// and stores a <see cref="WorkspaceEncryptionSession"/> (holding the plaintext Workspace DEK)
/// in the request items under <see cref="WorkspaceEncryptionSession.HttpContextName"/>.
/// Downstream file read/write paths resolve the DEK through
/// <c>StorageClientExtensions.GetEncryptionKey</c>, so they are unaware of the unlock plumbing.
/// </summary>
public class ValidateWorkspaceEncryptionSessionFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var workspace = context
            .HttpContext
            .GetWorkspaceMembershipDetails()
            .Workspace;

        if (workspace.Storage.EncryptionType != StorageEncryptionType.Full)
        {
            return await next(context);
        }

        var userExternalId = context.HttpContext.User.GetExternalId();
        var privateKey = UserEncryptionSessionCookie.TryReadPrivateKey(
            context.HttpContext, userExternalId);

        if (privateKey is null)
            return HttpErrors.Storage.UserEncryptionSessionRequired();

        var user = context.HttpContext.GetUserContext();

        var workspaceKeyReader = context.HttpContext.RequestServices
            .GetRequiredService<WorkspaceEncryptionKeyReader>();

        var wrappedDek = workspaceKeyReader.TryLoadWrappedDek(
            workspaceId: workspace.Id,
            userId: user.Id);

        if (wrappedDek is null)
        {
            Log.Warning(
                "User#{UserId} attempted to access full-encrypted Workspace#{WorkspaceId} but has no wrap in wek_workspace_encryption_keys.",
                user.Id, workspace.Id);

            return HttpErrors.Workspace.EncryptionAccessDenied(workspace.ExternalId);
        }

        byte[] dek;
        try
        {
            try
            {
                dek = UserKeyPair.OpenSealed(
                    recipientPrivateKey: privateKey,
                    sealed_: wrappedDek);
            }
            catch (Exception e)
            {
                // A corrupted or tamper-induced wrap will fail the sealed-box AEAD check.
                // Treat it the same as a missing wrap rather than leaking the unseal failure
                // (don't tell the client whether the row is absent or whether its contents
                // are broken — either way the user has no valid access).
                Log.Error(e,
                    "Unsealing wrapped Workspace DEK failed for User#{UserId} on Workspace#{WorkspaceId}.",
                    user.Id, workspace.Id);

                return HttpErrors.Workspace.EncryptionAccessDenied(workspace.ExternalId);
            }
        }
        finally
        {
            // The private key was needed only to open the sealed-box above. Wipe it from
            // the managed heap immediately — downstream endpoints consume the unwrapped
            // Workspace DEK via WorkspaceEncryptionSession, not the caller's private key.
            CryptographicOperations.ZeroMemory(privateKey);
        }

        context.HttpContext.Items[WorkspaceEncryptionSession.HttpContextName] =
            new WorkspaceEncryptionSession { WorkspaceDek = dek };

        return await next(context);
    }
}
