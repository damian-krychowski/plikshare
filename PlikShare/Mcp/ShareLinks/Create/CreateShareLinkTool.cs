using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PlikShare.Agents.Tools;
using PlikShare.AuditLog;
using PlikShare.Core.Clock;
using PlikShare.Mcp.ShareLinks.Create.Contracts;
using PlikShare.QuickShares;
using PlikShare.QuickShares.Create;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.ShareLinks.Create;

[McpServerToolType]
public class CreateShareLinkTool
{
    [McpServerTool(Name = AgentToolNames.CreateShareLink)]
    [Description("Creates a public link that shares the selected files and/or folders from a workspace the " +
                 "agent can access. Anyone with the link can download the shared items — no login required. " +
                 "Provide at least one id in fileExternalIds or folderExternalIds. Optional: expiresAt " +
                 "(ISO 8601), maxDownloads, password. Returns the share's external id and public URL.")]
    public static async Task<CreateShareLinkResponseDto> Execute(
        IHttpContextAccessor httpContextAccessor,
        WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
        CreateQuickShareQuery createQuickShareQuery,
        QuickShareUrlBuilder urlBuilder,
        AuditLogService auditLogService,
        IClock clock,
        [Description("External id of the workspace.")]
        string workspaceExternalId,
        [Description("Name for the share link.")]
        string name,
        [Description("External ids of files to share. Optional if folders are given.")]
        string[]? fileExternalIds = null,
        [Description("External ids of folders to share. Optional if files are given.")]
        string[]? folderExternalIds = null,
        [Description("Optional external ids of files to carve out of the shared folders (the folder is shared but these files are not).")]
        string[]? excludedFileExternalIds = null,
        [Description("Optional external ids of subfolders to carve out of the shared folders.")]
        string[]? excludedFolderExternalIds = null,
        [Description("Optional ISO 8601 timestamp after which the link stops working.")]
        string? expiresAt = null,
        [Description("Optional maximum number of downloads.")]
        int? maxDownloads = null,
        [Description("Optional password required to open the link.")]
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

        var trimmedName = (name ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(trimmedName))
            throw new McpException("A name for the share link is required.");

        var files = (fileExternalIds ?? []).ToList();
        var folders = (folderExternalIds ?? []).ToList();
        var excludedFiles = (excludedFileExternalIds ?? []).ToList();
        var excludedFolders = (excludedFolderExternalIds ?? []).ToList();

        if (files.Count == 0 && folders.Count == 0)
            throw new McpException("Provide at least one id in fileExternalIds or folderExternalIds.");

        DateTimeOffset? expires = null;

        if (!string.IsNullOrWhiteSpace(expiresAt))
        {
            if (!DateTimeOffset.TryParse(expiresAt, out var parsed))
                throw new McpException($"Invalid expiresAt '{expiresAt}'. Use an ISO 8601 timestamp.");

            if (parsed <= clock.UtcNow)
                throw new McpException("expiresAt must be in the future.");

            expires = parsed;
        }

        if (maxDownloads is <= 0)
            throw new McpException("maxDownloads must be greater than zero.");

        string? passwordHashBase64 = null;
        byte[]? passwordSalt = null;

        if (!string.IsNullOrEmpty(password))
        {
            var (hash, salt) = await QuickSharePasswordHasher.Hash(
                password);

            passwordHashBase64 = hash;
            passwordSalt = salt;
        }

        var agent = membership.Agent;

        var result = await createQuickShareQuery.Execute(
            workspace: workspace,
            creatorExternalId: agent.Owner.ExternalId,
            creatorAgentExternalId: agent.ExternalId,
            name: trimmedName,
            customSlug: null,
            selectedFiles: files,
            selectedFolders: folders,
            excludedFiles: excludedFiles,
            excludedFolders: excludedFolders,
            mode: QuickShareMode.Browser,
            allowIndividualFileDownload: true,
            expiresAt: expires,
            passwordHashBase64: passwordHashBase64,
            passwordSalt: passwordSalt,
            maxDownloads: maxDownloads,
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case CreateQuickShareQuery.ResultCode.Ok:
                var selectedCtx = auditLogService.GetBulkItemsContext(
                    folderExternalIds: folders,
                    fileExternalIds: files,
                    fileUploadExternalIds: []);

                var excludedCtx = auditLogService.GetBulkItemsContext(
                    folderExternalIds: excludedFolders,
                    fileExternalIds: excludedFiles,
                    fileUploadExternalIds: []);

                await auditLogService.Log(
                    Audit.QuickShare.CreatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: workspace.ToAuditLogWorkspaceRef(),
                        quickShare: new Audit.QuickShareRef
                        {
                            ExternalId = result.QuickShareExternalId,
                            Name = trimmedName
                        },
                        mode: QuickShareMode.Browser,
                        allowIndividualFileDownload: true,
                        hasPassword: passwordHashBase64 is not null,
                        maxDownloads: maxDownloads,
                        expiresAt: expires,
                        selectedFiles: selectedCtx.Files,
                        selectedFolders: selectedCtx.Folders,
                        excludedFiles: excludedCtx.Files,
                        excludedFolders: excludedCtx.Folders),
                    cancellationToken);

                return new CreateShareLinkResponseDto
                {
                    ExternalId = result.QuickShareExternalId.Value,
                    Url = urlBuilder.BuildUrl(result.Slug!)
                };

            case CreateQuickShareQuery.ResultCode.ItemsNotFound:
                throw new McpException(
                    "One or more of the specified files or folders were not found in the workspace.");

            case CreateQuickShareQuery.ResultCode.CreatorNotFound:
                throw new McpException("The share link creator could not be resolved.");

            default:
                throw new McpException($"Could not create the share link: {result.Code}.");
        }
    }
}
