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
/// Ensures the caller has unwrapped Workspace DEKs available in <see cref="HttpContext.Items"/>
/// for the target workspace when its storage uses full encryption. For None/Managed storages
/// the filter is a no-op.
///
/// The unlock is two-phase:
/// 1. The caller must hold a valid <see cref="UserEncryptionSessionCookie"/> carrying their
///    DPAPI-protected X25519 private key. If not, we short-circuit with 423
///    <c>user-encryption-session-required</c> so the client can prompt for the encryption
///    password.
/// 2. The caller must own at least one per-user wrap of this workspace's DEK in
///    <c>wek_workspace_encryption_keys</c>. Absence means the user is authenticated and
///    unlocked but is not a member with encrypted-file access to this workspace — a 403
///    <c>workspace-encryption-access-denied</c>.
///
/// The actual load + unseal work lives in <see cref="UserWorkspaceDekUnsealer"/>. This filter
/// only handles HTTP plumbing: cookie read, private-key wiping, and mapping the unsealer's
/// result codes to the matching HTTP responses.
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

        var userExternalId = context
            .HttpContext
            .User
            .GetExternalId();

        var privateKey = UserEncryptionSessionCookie.TryReadPrivateKey(
            context.HttpContext, 
            userExternalId);

        if (privateKey is null)
            return HttpErrors.Storage.UserEncryptionSessionRequired();

        var user = await context.HttpContext.GetUserContext();

        WorkspaceDekEntry[] entries;
        try
        {
            var unsealer = context.HttpContext.RequestServices
                .GetRequiredService<UserWorkspaceDekUnsealer>();

            var result = unsealer.UnsealForUser(
                workspaceId: workspace.Id,
                userId: user.Id,
                privateKey: privateKey);

            switch (result.Code)
            {
                case UserWorkspaceDekUnsealer.ResultCode.NoWraps:
                    Log.Warning(
                        "User#{UserId} attempted to access full-encrypted Workspace#{WorkspaceId} but has no wraps in wek_workspace_encryption_keys.",
                        user.Id, workspace.Id);

                    return HttpErrors.Workspace.EncryptionAccessDenied(workspace.ExternalId);

                case UserWorkspaceDekUnsealer.ResultCode.UnsealFailed:
                    // The unsealer already logged the specifics. We hide the distinction
                    // between missing-wrap and corrupted-wrap behind the same 403.
                    return HttpErrors.Workspace.EncryptionAccessDenied(workspace.ExternalId);

                case UserWorkspaceDekUnsealer.ResultCode.Ok:
                    entries = result.Entries!;
                    break;

                default:
                    throw new UnexpectedOperationResultException(
                        operationName: nameof(UserWorkspaceDekUnsealer),
                        resultValueStr: result.Code.ToString());
            }
        }
        finally
        {
            // The private key was needed only by the unsealer. Wipe it from the managed heap
            // immediately — downstream endpoints consume the unwrapped Workspace DEKs via
            // WorkspaceEncryptionSession, not the caller's private key.
            CryptographicOperations.ZeroMemory(privateKey);
        }

        context.HttpContext.Items[WorkspaceEncryptionSession.HttpContextName] =
            new WorkspaceEncryptionSession(entries);

        return await next(context);
    }
}
