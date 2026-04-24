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
/// only handles HTTP plumbing: cookie read and mapping the unsealer's result codes to the
/// matching HTTP responses. The private key's lifetime is bound by <c>using</c> to this
/// method; the workspace session's lifetime is bound to the HTTP response via
/// <see cref="HttpResponse.RegisterForDispose"/>.
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

        if (workspace.Storage.Encryption.Type != StorageEncryptionType.Full)
        {
            return await next(context);
        }

        var userExternalId = context
            .HttpContext
            .User
            .GetExternalId();

        // Cheap cookie-presence check first — lets us hand the right error back to the UI
        // (setup vs unlock dialog) without paying DataProtection cost when there's no
        // cookie at all.
        if (!UserEncryptionSessionCookie.IsPresent(context.HttpContext, userExternalId))
        {
            var caller = await context.HttpContext.GetUserContext();

            return caller.EncryptionMetadata is null
                ? HttpErrors.Storage.UserEncryptionSetupRequired()
                : HttpErrors.Storage.UserEncryptionSessionRequired();
        }

        using var privateKey = UserEncryptionSessionCookie.TryReadPrivateKey(
            context.HttpContext,
            userExternalId);

        if (privateKey is null)
        {
            // Cookie existed but failed to unprotect (stale data-protection key, tamper,
            // truncation). Treat as "session gone" and make the user unlock again — the
            // key material in DB is intact, so setup is not appropriate here.
            return HttpErrors.Storage.UserEncryptionSessionRequired();
        }

        var user = await context.HttpContext.GetUserContext();

        var unsealer = context
            .HttpContext
            .RequestServices
            .GetRequiredService<UserWorkspaceDekUnsealer>();

        var result = unsealer.UnsealForUser(
            workspaceId: workspace.Id,
            userId: user.Id,
            privateKey: privateKey);

        switch (result.Code)
        {
            case UserWorkspaceDekUnsealer.ResultCode.NoWraps:
                Log.Information(
                    "User#{UserId} is a member of full-encrypted Workspace#{WorkspaceId} but has no wraps yet — pending owner key grant.",
                    user.Id, workspace.Id);

                return HttpErrors.Workspace.PendingKeyGrant(workspace.ExternalId);

            case UserWorkspaceDekUnsealer.ResultCode.UnsealFailed:
                // The unsealer already logged the specifics. We hide the distinction
                // between missing-wrap and corrupted-wrap behind the same 403.
                return HttpErrors.Workspace.EncryptionAccessDenied(workspace.ExternalId);

            case UserWorkspaceDekUnsealer.ResultCode.Ok:
                {
                    var session = new WorkspaceEncryptionSession(
                        workspaceId: workspace.Id,
                        entries: result.Entries!);

                    context.HttpContext.Response.RegisterForDispose(session);
                    context.HttpContext.Items[WorkspaceEncryptionSession.HttpContextName] = session;

                    break;
                }

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(UserWorkspaceDekUnsealer),
                    resultValueStr: result.Code.ToString());
        }

        return await next(context);
    }
}