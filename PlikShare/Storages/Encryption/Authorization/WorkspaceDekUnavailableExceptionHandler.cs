using Microsoft.AspNetCore.Diagnostics;
using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Workspaces.Id;
using Serilog;

namespace PlikShare.Storages.Encryption.Authorization;

/// <summary>
/// Translates <see cref="WorkspaceDekForVersionNotAvailableException"/> — thrown by
/// <see cref="WorkspaceEncryptionSession.GetDekForVersion"/> when a deep encryption/decryption
/// path asks for a Storage DEK version the caller's session does not carry — into an HTTP 403
/// <c>workspace-encryption-access-denied</c> response instead of the default 500.
///
/// The exception signals a genuine user-level access gap (e.g. file written before they
/// joined the workspace, or rotation that has not yet backfilled them), not a server bug,
/// so returning the same code the filter uses for "not a workspace encryption member" keeps
/// the client UX coherent.
///
/// Registered via <c>builder.Services.AddExceptionHandler&lt;WorkspaceDekUnavailableExceptionHandler&gt;()</c>
/// in Startup, together with <c>app.UseExceptionHandler()</c>.
/// </summary>
public class WorkspaceDekUnavailableExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not WorkspaceDekForVersionNotAvailableException dekException)
            return false;

        var workspaceExternalId = TryExtractWorkspaceExternalId(httpContext);

        Log.Warning(
            "Workspace DEK for version {RequestedVersion} unavailable on Workspace '{WorkspaceExternalId}' — " +
            "session carries [{AvailableVersions}]. Returning 403.",
            dekException.RequestedStorageDekVersion,
            workspaceExternalId?.ToString() ?? "(unknown)",
            dekException.AvailableStorageDekVersions);

        var result = workspaceExternalId is not null
            ? HttpErrors.Workspace.EncryptionAccessDenied(workspaceExternalId.Value)
            : Microsoft.AspNetCore.Http.Results.StatusCode(StatusCodes.Status403Forbidden);

        await result.ExecuteAsync(httpContext);
        return true;
    }

    private static WorkspaceExtId? TryExtractWorkspaceExternalId(HttpContext httpContext)
    {
        // The workspace-scoped endpoints use {workspaceExternalId} in their route; presigned
        // URL paths don't, so this is best-effort. When it's missing we fall back to a plain
        // 403 with no body — still the right status code, just without the workspace id.
        var raw = httpContext.Request.RouteValues["workspaceExternalId"]?.ToString();
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        return WorkspaceExtId.TryParse(raw, null, out var parsed) ? parsed : null;
    }
}
