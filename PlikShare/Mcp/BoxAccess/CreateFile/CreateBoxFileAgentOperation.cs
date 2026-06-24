using System.Text;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using PlikShare.Agents.BoxAccess;
using PlikShare.Agents.Middleware;
using PlikShare.AuditLog;
using PlikShare.Boxes.Cache;
using PlikShare.Boxes.Id;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Files.Records;
using PlikShare.Folders.Id;
using PlikShare.Mcp.BoxAccess.CreateFile.Contracts;
using PlikShare.Mcp.Files.Create;
using PlikShare.Storages;
using PlikShare.Uploads.Algorithm;
using PlikShare.Workspaces.Cache;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.BoxAccess.CreateFile;

/// <summary>
/// The reusable core of create_box_file: re-validates the agent's box access, resolves the target folder
/// inside the box (defaulting to the box root, scoped to the box's subtree), stores the inline UTF-8 content
/// as a new file and writes the audit entry. Called directly by the tool when no approval is required, and
/// by the execute flow once a human has approved the operation.
/// </summary>
public class CreateBoxFileAgentOperation(
    BoxCache boxCache,
    AgentBoxAccessCache boxAccessCache,
    PlikShareDb plikShareDb,
    InsertCompletedFileQuery insertCompletedFileQuery,
    WorkspaceSizeCache workspaceSizeCache,
    AuditLogService auditLogService)
{
    public async Task<CreateBoxFileResponseDto> Execute(
        HttpContext httpContext,
        CreateBoxFileParams parameters,
        CancellationToken cancellationToken)
    {
        var agent = await httpContext.GetAgentContext();

        var boxAccess = await httpContext.GetAgentBoxAccess(
            agent,
            boxCache,
            boxAccessCache,
            BoxExtId.Parse(parameters.BoxExternalId),
            cancellationToken);

        if (boxAccess.IsOff)
            throw new McpException(
                $"Box '{parameters.BoxExternalId}' is disabled or exposes no folder, so files cannot be created in it.");

        var workspace = boxAccess.Box.Workspace;
        var boxFolder = boxAccess.Box.Folder!;

        var nameParts = FileNames.TryGetNameAndExtension(parameters.Name);

        if (nameParts is null)
            throw new McpException("The file name is invalid. Provide a non-empty file name with an extension.");

        var content = Encoding.UTF8.GetBytes(parameters.Content ?? string.Empty);

        if (content.Length > CreateFileAgentOperation.MaximumContentSizeInBytes)
            throw new McpException(
                $"The content is too large. create_box_file accepts at most {CreateFileAgentOperation.MaximumContentSizeInBytes} bytes.");

        var (folderId, folderExternalId) = ResolveTargetFolder(
            workspace,
            boxFolder.Id,
            boxFolder.ExternalId,
            parameters.FolderExternalId,
            parameters.BoxExternalId);

        var currentSize = workspaceSizeCache.Get(workspace.Id);

        if (workspace.MaxSizeInBytes is { } maxSize && currentSize + content.Length > maxSize)
            throw new McpException("The box's workspace does not have enough free space to store this file.");

        var resolvedContentType = string.IsNullOrWhiteSpace(parameters.ContentType)
            ? ResolveContentType(nameParts.Extension)
            : parameters.ContentType.Trim();

        var fileKey = workspace.Storage.GenerateFileKey();
        var encryptionMetadata = workspace.GenerateFileEncryptionMetadata();
        var encryptionMode = workspace.GetFileEncryptionMode(encryptionMetadata, null);

        await workspace.UploadFilePart(
            input: content,
            uploadDetails: new UploadFilePartDetails(
                FileKey: fileKey,
                MultipartUploadId: null,
                FileSizeInBytes: content.Length,
                Part: FilePart.First(content.Length),
                EncryptionMode: encryptionMode,
                UploadAlgorithm: UploadAlgorithm.DirectUpload),
            cancellationToken: cancellationToken);

        var insertResult = await insertCompletedFileQuery.Execute(
            workspace: workspace,
            folderId: folderId,
            file: new InsertCompletedFileQuery.NewFile
            {
                ExternalId = fileKey.FileExternalId,
                KeySecretPart = fileKey.KeySecretPart,
                Name = workspace.EncodeMetadata(nameParts.Name, null),
                Extension = workspace.EncodeMetadata(nameParts.Extension, null),
                ContentType = workspace.EncodeMetadata(resolvedContentType, null),
                SizeInBytes = content.Length,
                EncryptionMetadata = encryptionMetadata
            },
            uploader: boxAccess.UserIdentity,
            cancellationToken: cancellationToken);

        if (insertResult == InsertCompletedFileQuery.ResultCode.FolderNotFound)
            throw new McpException(
                $"Folder '{folderExternalId}' was not found inside box '{parameters.BoxExternalId}'.");

        workspaceSizeCache.AddDelta(workspace.Id, content.Length);

        await auditLogService.Log(
            Audit.Agent.FileCreatedEntry(
                actor: httpContext.GetAuditLogActorContext(),
                workspaceExternalId: workspace.ExternalId.Value,
                fileExternalId: fileKey.FileExternalId.Value,
                folderExternalId: folderExternalId,
                sizeInBytes: content.Length),
            cancellationToken);

        return new CreateBoxFileResponseDto
        {
            FileExternalId = fileKey.FileExternalId.Value,
            Name = parameters.Name,
            FolderExternalId = folderExternalId
        };
    }

    // Resolves the folder the file goes into and guarantees it lives inside the box: when no folder is
    // given the box root is used; otherwise the folder must be the box root or one of its descendants.
    private (int? FolderId, string FolderExternalId) ResolveTargetFolder(
        WorkspaceContext workspace,
        int boxFolderId,
        FolderExtId boxFolderExternalId,
        string? requestedFolderExternalId,
        string boxExternalId)
    {
        if (string.IsNullOrWhiteSpace(requestedFolderExternalId))
            return (boxFolderId, boxFolderExternalId.Value);

        using var connection = plikShareDb.OpenConnection();

        var result = connection
            .OneRowCmd(
                sql: """
                     SELECT fo_id
                     FROM fo_folders
                     WHERE fo_external_id = $folderExternalId
                         AND fo_workspace_id = $workspaceId
                         AND fo_is_being_deleted = FALSE
                         AND (
                             fo_id = $boxFolderId
                             OR $boxFolderId IN (
                                 SELECT value FROM json_each(fo_ancestor_folder_ids)
                             )
                         )
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$folderExternalId", requestedFolderExternalId)
            .WithParameter("$workspaceId", workspace.Id)
            .WithParameter("$boxFolderId", boxFolderId)
            .Execute();

        if (result.IsEmpty)
            throw new McpException(
                $"Folder '{requestedFolderExternalId}' was not found inside box '{boxExternalId}'.");

        return (result.Value, requestedFolderExternalId);
    }

    private static string ResolveContentType(string extension)
    {
        var key = extension.TrimStart('.').ToLowerInvariant();

        return key switch
        {
            "txt" or "text" or "log" => "text/plain",
            "md" or "markdown" => "text/markdown",
            "json" or "jsonl" or "ndjson" => "application/json",
            "csv" => "text/csv",
            "tsv" => "text/tab-separated-values",
            "xml" => "application/xml",
            "html" or "htm" => "text/html",
            "css" => "text/css",
            "yaml" or "yml" => "application/yaml",
            "js" or "mjs" => "text/javascript",
            "sql" => "application/sql",
            _ => "text/plain"
        };
    }
}
