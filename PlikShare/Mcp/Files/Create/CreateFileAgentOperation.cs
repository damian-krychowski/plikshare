using System.Text;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using PlikShare.AuditLog;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Files.Records;
using PlikShare.Mcp.Files.Create.Contracts;
using PlikShare.Storages;
using PlikShare.Uploads.Algorithm;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.Files.Create;

/// <summary>
/// The reusable core of create_file: re-validates the agent's workspace access, stores the inline
/// UTF-8 content as a new file and writes the audit entry. Called directly by the tool when no
/// approval is required, and by the execute flow once a human has approved the operation.
/// </summary>
public class CreateFileAgentOperation(
    WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
    PlikShareDb plikShareDb,
    InsertCompletedFileQuery insertCompletedFileQuery,
    WorkspaceSizeCache workspaceSizeCache,
    AuditLogService auditLogService)
{
    public const int MaximumContentSizeInBytes = Aes256GcmStreamingV1.MaximumPayloadSize;

    public async Task<CreateFileResponseDto> Execute(
        HttpContext httpContext,
        CreateFileParams parameters,
        CancellationToken cancellationToken)
    {
        var membership = await httpContext.GetWorkspaceAgentMembershipDetails(
            workspaceAgentMembershipCache,
            WorkspaceExtId.Parse(parameters.WorkspaceExternalId),
            cancellationToken);

        var workspace = membership.Workspace;
        workspace.ThrowIfFullyEncrypted();

        var nameParts = FileNames.TryGetNameAndExtension(parameters.Name);

        if (nameParts is null)
            throw new McpException("The file name is invalid. Provide a non-empty file name with an extension.");

        var content = Encoding.UTF8.GetBytes(parameters.Content ?? string.Empty);

        if (content.Length > MaximumContentSizeInBytes)
            throw new McpException(
                $"The content is too large. create_file accepts at most {MaximumContentSizeInBytes} bytes.");

        int? folderId = null;

        if (parameters.FolderExternalId is not null)
        {
            folderId = ResolveFolderId(workspace, parameters.FolderExternalId);

            if (folderId is null)
                throw new McpException(
                    $"Folder '{parameters.FolderExternalId}' was not found in workspace '{parameters.WorkspaceExternalId}'.");
        }

        var currentSize = workspaceSizeCache.Get(workspace.Id);

        if (workspace.MaxSizeInBytes is { } maxSize && currentSize + content.Length > maxSize)
            throw new McpException("The workspace does not have enough free space to store this file.");

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
            uploader: new AgentIdentity(membership.Agent.ExternalId),
            cancellationToken: cancellationToken);

        if (insertResult == InsertCompletedFileQuery.ResultCode.FolderNotFound)
            throw new McpException(
                $"Folder '{parameters.FolderExternalId}' was not found in workspace '{parameters.WorkspaceExternalId}'.");

        workspaceSizeCache.AddDelta(workspace.Id, content.Length);

        await auditLogService.Log(
            Audit.Agent.FileCreatedEntry(
                actor: httpContext.GetAuditLogActorContext(),
                workspaceExternalId: parameters.WorkspaceExternalId,
                fileExternalId: fileKey.FileExternalId.Value,
                folderExternalId: parameters.FolderExternalId,
                sizeInBytes: content.Length),
            cancellationToken);

        return new CreateFileResponseDto
        {
            FileExternalId = fileKey.FileExternalId.Value
        };
    }

    private int? ResolveFolderId(WorkspaceContext workspace, string folderExternalId)
    {
        using var connection = plikShareDb.OpenConnection();

        var result = connection
            .OneRowCmd(
                sql: """
                     SELECT fo_id
                     FROM fo_folders
                     WHERE fo_external_id = $folderExternalId
                         AND fo_workspace_id = $workspaceId
                         AND fo_is_being_deleted = FALSE
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$folderExternalId", folderExternalId)
            .WithParameter("$workspaceId", workspace.Id)
            .Execute();

        return result.IsEmpty ? null : result.Value;
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
