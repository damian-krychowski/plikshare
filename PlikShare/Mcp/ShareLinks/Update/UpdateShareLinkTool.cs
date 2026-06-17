using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PlikShare.Agents.Tools;
using PlikShare.AuditLog;
using PlikShare.Core.Clock;
using PlikShare.QuickShares;
using PlikShare.QuickShares.Cache;
using PlikShare.QuickShares.Id;
using PlikShare.QuickShares.Update;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.ShareLinks.Update;

[McpServerToolType]
public class UpdateShareLinkTool
{
    [McpServerTool(Name = AgentToolNames.UpdateShareLink)]
    [Description("Updates settings of an existing share link in a workspace the agent can access. Only the " +
                 "fields you choose are changed; anything left out is kept. Pass name to rename. For a " +
                 "nullable setting (expiry, max downloads, password) set its shouldUpdate* flag to true and " +
                 "provide the new value to set it, or set the flag to true and leave the value empty to clear " +
                 "it (no expiry / unlimited downloads / no password).")]
    public static async Task Execute(
        IHttpContextAccessor httpContextAccessor,
        WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
        QuickShareCache quickShareCache,
        UpdateQuickShareQuery updateQuickShareQuery,
        AuditLogService auditLogService,
        IClock clock,
        [Description("External id of the workspace.")]
        string workspaceExternalId,
        [Description("External id of the share link to update.")]
        string shareLinkExternalId,
        [Description("Optional new name. Omit to keep the current name.")]
        string? name = null,
        [Description("Set true to change the expiry. Provide expiresAt to set it, or leave expiresAt empty to remove the expiry.")]
        bool shouldUpdateExpiry = false,
        [Description("New ISO 8601 expiry timestamp; used only when shouldUpdateExpiry is true. Empty clears the expiry.")]
        string? expiresAt = null,
        [Description("Set true to change the download limit. Provide maxDownloads to set it, or leave it empty for unlimited.")]
        bool shouldUpdateMaxDownloads = false,
        [Description("New maximum number of downloads; used only when shouldUpdateMaxDownloads is true. Empty means unlimited.")]
        int? maxDownloads = null,
        [Description("Set true to change the password. Provide password to set it, or leave it empty to remove the password.")]
        bool shouldUpdatePassword = false,
        [Description("New password; used only when shouldUpdatePassword is true. Empty removes the password.")]
        string? password = null,
        CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext!;

        var membership = await httpContext.GetWorkspaceAgentMembershipDetails(
            workspaceAgentMembershipCache,
            WorkspaceExtId.Parse(workspaceExternalId),
            cancellationToken);

        var workspace = membership.Workspace;
        workspace.ThrowIfFullyEncrypted();

        var quickShare = await quickShareCache.TryGetQuickShare(
            new QuickShareExtId(shareLinkExternalId),
            cancellationToken);

        if (quickShare is null || quickShare.Workspace.Id != workspace.Id)
            throw new McpException(
                $"Share link '{shareLinkExternalId}' was not found in workspace '{workspaceExternalId}'.");

        var updateName = name is not null;
        string? trimmedName = null;

        if (updateName)
        {
            trimmedName = name!.Trim();

            if (string.IsNullOrWhiteSpace(trimmedName))
                throw new McpException("The share link name cannot be empty.");
        }

        if (!updateName && !shouldUpdateExpiry && !shouldUpdateMaxDownloads && !shouldUpdatePassword)
            throw new McpException(
                "Provide at least one field to update (name, expiry, max downloads or password).");

        DateTimeOffset? expires = null;

        if (shouldUpdateExpiry && !string.IsNullOrWhiteSpace(expiresAt))
        {
            if (!DateTimeOffset.TryParse(expiresAt, out var parsed))
                throw new McpException($"Invalid expiresAt '{expiresAt}'. Use an ISO 8601 timestamp.");

            if (parsed <= clock.UtcNow)
                throw new McpException("expiresAt must be in the future.");

            expires = parsed;
        }

        int? newMaxDownloads = null;

        if (shouldUpdateMaxDownloads && maxDownloads is not null)
        {
            if (maxDownloads <= 0)
                throw new McpException("maxDownloads must be greater than zero.");

            newMaxDownloads = maxDownloads;
        }

        string? passwordHashBase64 = null;
        byte[]? passwordSalt = null;
        var passwordSet = false;

        if (shouldUpdatePassword && !string.IsNullOrEmpty(password))
        {
            var (hash, salt) = await QuickSharePasswordHasher.Hash(
                password);

            passwordHashBase64 = hash;
            passwordSalt = salt;
            passwordSet = true;
        }

        var resultCode = await updateQuickShareQuery.Execute(
            quickShare: quickShare,
            updateName: updateName,
            name: trimmedName,
            updateExpiration: shouldUpdateExpiry,
            expiresAt: expires,
            updateMaxDownloads: shouldUpdateMaxDownloads,
            maxDownloads: newMaxDownloads,
            updatePassword: shouldUpdatePassword,
            passwordHashBase64: passwordHashBase64,
            passwordSalt: passwordSalt,
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case UpdateQuickShareQuery.ResultCode.Ok:
                await quickShareCache.InvalidateEntry(
                    quickShareId: quickShare.Id,
                    cancellationToken: cancellationToken);

                await auditLogService.Log(
                    Audit.QuickShare.UpdatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: quickShare.Workspace.ToAuditLogWorkspaceRef(),
                        quickShare: quickShare.ToAuditLogQuickShareRef(),
                        nameUpdated: updateName,
                        expirationUpdated: shouldUpdateExpiry,
                        expiresAt: expires,
                        maxDownloadsUpdated: shouldUpdateMaxDownloads,
                        maxDownloads: newMaxDownloads,
                        passwordUpdated: shouldUpdatePassword,
                        passwordSet: passwordSet),
                    cancellationToken);

                return;

            case UpdateQuickShareQuery.ResultCode.NotFound:
                throw new McpException(
                    $"Share link '{shareLinkExternalId}' was not found in workspace '{workspaceExternalId}'.");

            default:
                throw new McpException($"Could not update the share link: {resultCode}.");
        }
    }
}
