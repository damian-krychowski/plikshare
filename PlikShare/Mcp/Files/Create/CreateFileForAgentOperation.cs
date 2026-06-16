using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Storages;
using PlikShare.Files.Records;
using PlikShare.Uploads.Algorithm;
using PlikShare.Workspaces.Cache;

namespace PlikShare.Mcp.Files.Create;

public class CreateFileForAgentOperation(
    PlikShareDb plikShareDb,
    InsertCompletedFileQuery insertCompletedFileQuery,
    WorkspaceSizeCache workspaceSizeCache)
{
    public const int MaximumContentSizeInBytes = Aes256GcmStreamingV1.MaximumPayloadSize;

    public async Task<Result> Execute(
        WorkspaceContext workspace,
        IUserIdentity uploader,
        string? folderExternalId,
        string name,
        byte[] content,
        string? contentType,
        CancellationToken cancellationToken)
    {
        var nameParts = FileNames.TryGetNameAndExtension(name);

        if (nameParts is null)
            return new Result(ResultCode.InvalidName);

        if (content.Length > MaximumContentSizeInBytes)
            return new Result(ResultCode.ContentTooLarge);

        int? folderId = null;

        if (folderExternalId is not null)
        {
            folderId = ResolveFolderId(workspace, folderExternalId);

            if (folderId is null)
                return new Result(ResultCode.FolderNotFound);
        }

        var currentSize = workspaceSizeCache.Get(workspace.Id);

        if (workspace.MaxSizeInBytes is { } maxSize && currentSize + content.Length > maxSize)
            return new Result(ResultCode.NotEnoughSpace);

        var resolvedContentType = string.IsNullOrWhiteSpace(contentType)
            ? ResolveContentType(nameParts.Extension)
            : contentType.Trim();

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
            uploader: uploader,
            cancellationToken: cancellationToken);

        if (insertResult == InsertCompletedFileQuery.ResultCode.FolderNotFound)
            return new Result(ResultCode.FolderNotFound);

        workspaceSizeCache.AddDelta(workspace.Id, content.Length);

        return new Result(
            Code: ResultCode.Ok,
            FileExternalId: fileKey.FileExternalId.Value);
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

    public readonly record struct Result(
        ResultCode Code,
        string? FileExternalId = null);

    public enum ResultCode
    {
        Ok = 0,
        InvalidName,
        ContentTooLarge,
        FolderNotFound,
        NotEnoughSpace
    }
}
