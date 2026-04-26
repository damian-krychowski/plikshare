using PlikShare.Core.Authorization;
using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Users.Middleware;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Storages.Encryption.Authorization;

public static class HttpContextWorkspaceEncryptionSessionExtensions
{
    extension(HttpContext httpContext)
    {
        public WorkspaceEncryptionSession? TryGetWorkspaceEncryptionSession()
        {
            return httpContext.Items[WorkspaceEncryptionSession.HttpContextName]
                as WorkspaceEncryptionSession;
        }

        public EncryptableMetadata ToEncryptable(string value)
        {
            var wes = httpContext.TryGetWorkspaceEncryptionSession();
            return wes.ToEncryptableMetadata(value);
        }

        public async ValueTask<StartWorkspaceEncryptionSessionResult> TryStartWorkspaceEncryptionSession(
            WorkspaceContext workspace)
        {
            if (workspace.Storage.Encryption.Type != StorageEncryptionType.Full)
            {
                return new StartWorkspaceEncryptionSessionResult(
                    StartWorkspaceEncryptionSessionResultCode.WorkspaceIsNotEncrypted);
            }

            var userExternalId = httpContext
                .User
                .GetExternalId();

            var user = await httpContext.GetUserContext();

            // Cheap cookie-presence check first — lets us hand the right error back to the UI
            // (setup vs unlock dialog) without paying DataProtection cost when there's no
            // cookie at all.
            if (!UserEncryptionSessionCookie.IsPresent(httpContext, userExternalId))
            {
                return user.EncryptionMetadata is null
                    ? new StartWorkspaceEncryptionSessionResult(
                        StartWorkspaceEncryptionSessionResultCode.UserEncryptionSetupRequired)
                    : new StartWorkspaceEncryptionSessionResult(
                        StartWorkspaceEncryptionSessionResultCode.UserEncryptionSessionRequired);
            }

            using var privateKey = UserEncryptionSessionCookie.TryReadPrivateKey(
                httpContext,
                userExternalId);

            if (privateKey is null)
            {
                // Cookie existed but failed to unprotect (stale data-protection key, tamper,
                // truncation). Treat as "session gone" and make the user unlock again — the
                // key material in DB is intact, so setup is not appropriate here.
                return new StartWorkspaceEncryptionSessionResult(
                    StartWorkspaceEncryptionSessionResultCode.UserEncryptionSessionRequired);
            }

            try
            {
                var workspaceDeks = user.UnsealWorkspaceDeks(
                    workspace: workspace,
                    privateKey: privateKey);

                if (workspaceDeks.Length == 0)
                {
                    Log.Information(
                        "User#{UserId} is a member of full-encrypted Workspace#{WorkspaceId} but has no workspace " +
                        "wraps and no storage wraps — pending owner key grant.",
                        user.Id, workspace.Id);

                    return new StartWorkspaceEncryptionSessionResult(
                        StartWorkspaceEncryptionSessionResultCode.PendingKeyGrant);
                }

                return new StartWorkspaceEncryptionSessionResult(
                    Code: StartWorkspaceEncryptionSessionResultCode.Ok,
                    WorkspaceEncryptionSession: new WorkspaceEncryptionSession(
                        workspaceId: workspace.Id,
                        entries: workspaceDeks));

            }
            catch (StorageDekUnsealException e)
            {
                // A corrupted or tamper-induced wrap will fail the sealed-box AEAD check.
                // Treat the whole unseal as failed and wipe anything we already produced.
                Log.Error(e,
                    "Unsealing wrapped Storage DEK v{Version} failed for User#{UserId} on Storage#{StorageId}.",
                    e.StorageDekVersion, user.Id, e.StorageId);

                // The unsealer already logged the specifics. We hide the distinction
                // between missing-wrap and corrupted-wrap behind the same 403.
                return new StartWorkspaceEncryptionSessionResult(
                    StartWorkspaceEncryptionSessionResultCode.EncryptionAccessDenied);
            }
            catch (WorkspaceDekUnsealException e)
            {
                // A corrupted or tamper-induced wrap will fail the sealed-box AEAD check.
                // Treat the whole unseal as failed and wipe anything we already produced.
                Log.Error(e,
                    "Unsealing wrapped Workspace DEK v{Version} failed for User#{UserId} on Workspace#{WorkspaceId}.",
                    e.StorageDekVersion, user.Id, e.WorkspaceId);

                // The unsealer already logged the specifics. We hide the distinction
                // between missing-wrap and corrupted-wrap behind the same 403.
                return new StartWorkspaceEncryptionSessionResult(
                    StartWorkspaceEncryptionSessionResultCode.EncryptionAccessDenied);
            }
        }
    }
}

public readonly record struct StartWorkspaceEncryptionSessionResult(
    StartWorkspaceEncryptionSessionResultCode Code,
    WorkspaceEncryptionSession? WorkspaceEncryptionSession = null);
    
public enum StartWorkspaceEncryptionSessionResultCode
{
    Ok = 0,
    WorkspaceIsNotEncrypted,
    UserEncryptionSetupRequired,
    UserEncryptionSessionRequired,
    PendingKeyGrant,
    EncryptionAccessDenied
}