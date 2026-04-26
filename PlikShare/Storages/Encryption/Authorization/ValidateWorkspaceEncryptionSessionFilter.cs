using PlikShare.Core.Authorization;
using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Users.Middleware;
using PlikShare.Workspaces.Validation;
using Serilog;

namespace PlikShare.Storages.Encryption.Authorization;

/// <summary>
/// Ensures the caller has unwrapped Workspace DEKs available in <see cref="HttpContext.Items"/>
/// for the target workspace when its storage uses full encryption. For None/Managed storages
/// the filter is a no-op.
///
/// The unlock is two-phase:
/// 1. The caller must hold a valid <see cref="UserEncryptionSessionCookie"/> carrying their
///    DPAPI-protected X25519 private key. If not, we short-circuit with 423
///    <c>user-encryption-session-required</c> so the client can prompt for the encryption
///    password.
/// 2. The caller must be able to produce at least one Workspace DEK for this workspace,
///    via either of two paths:
///      a) a per-user wrap in <c>wek_workspace_encryption_keys</c> (member path), or
///      b) a per-user wrap of the Storage DEK in <c>sek_storage_encryption_keys</c>
///         (storage-owner path) — in that case the Workspace DEK is derived on the fly
///         from (Storage DEK, workspace salt) via <see cref="StorageDekEntry.DeriveWorkspaceDek"/>.
///    When merging both paths, workspace wraps win for any (StorageDekVersion) collision —
///    they are the authoritative source. Storage-derived entries fill the gaps so a
///    storage owner can read every file they could decrypt with the recovery code, even
///    before a workspace member wrap has been provisioned for them.
///    Absence of both means the user is authenticated and unlocked but is not a member
///    with encrypted-file access to this workspace — a 403
///    <c>workspace-encryption-access-denied</c>.
///
/// The actual load + unseal + storage-fallback merge work lives in
/// <see cref="UserContextEncryptionExtensions.UnsealWorkspaceDeks"/>. This filter
/// only handles HTTP plumbing: cookie read and mapping unsealer result codes to
/// the matching HTTP responses. The private key's lifetime is bound by <c>using</c>
/// to this method; the workspace session's lifetime is bound to the HTTP response
/// via <see cref="HttpResponse.RegisterForDispose"/>.
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

        var (code, session) = await context.HttpContext.TryStartWorkspaceEncryptionSession(
            workspace);

        switch (code)
        {
            case StartWorkspaceEncryptionSessionResultCode.Ok:
            {
                context.HttpContext.Response.RegisterForDispose(session!);
                context.HttpContext.Items[WorkspaceEncryptionSession.HttpContextName] = session;

                return await next(context);
            }

            case StartWorkspaceEncryptionSessionResultCode.WorkspaceIsNotEncrypted:
                return await next(context);

            case StartWorkspaceEncryptionSessionResultCode.UserEncryptionSetupRequired:
                return HttpErrors.Storage.UserEncryptionSetupRequired();

            case StartWorkspaceEncryptionSessionResultCode.UserEncryptionSessionRequired:
                return HttpErrors.Storage.UserEncryptionSessionRequired();

            case StartWorkspaceEncryptionSessionResultCode.PendingKeyGrant:
                return HttpErrors.Workspace.PendingKeyGrant(workspace.ExternalId);

            case StartWorkspaceEncryptionSessionResultCode.EncryptionAccessDenied:
                return HttpErrors.Workspace.EncryptionAccessDenied(workspace.ExternalId);

            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}