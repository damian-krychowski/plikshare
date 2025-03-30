using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Core.UserIdentity;
using PlikShare.Uploads.Id;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.UpdateCurrentSizeInBytes.QueueJob;
using Serilog;

namespace PlikShare.Uploads.Initiate;

public class BulkInsertFileUploadQuery(DbWriteQueue dbWriteQueue)
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<BulkInsertFileUploadQuery>();

    public Task<Result> Execute(
        WorkspaceContext workspace,
        IUserIdentity userIdentity,
        InsertEntity[] entities,
        long newWorkspaceSizeInBytes, //new workspace size needs to be precalculated before calling this endpoint (performance)
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                workspace: workspace,
                userIdentity: userIdentity,
                entities: entities,
                newWorkspaceSizeInBytes: newWorkspaceSizeInBytes),
            cancellationToken: cancellationToken);
    }

    private Result ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        WorkspaceContext workspace,
        IUserIdentity userIdentity,
        InsertEntity[] entities,
        long newWorkspaceSizeInBytes)
    {
        dbWriteContext.Connection.RegisterJsonArrayToBlobFunction();
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var fileUploads = InsertFileUploads(
                workspace, 
                userIdentity, 
                entities,
                dbWriteContext,
                transaction);

            UpdateWorkspaceCurrentSizeInBytesQuery.Execute(
                workspaceId: workspace.Id,
                currentSizeInBytes: newWorkspaceSizeInBytes,
                dbWriteContext: dbWriteContext,
                transaction: transaction);

            transaction.Commit();

            //todo add some useful log

            return new Result(
                Code: ResultCode.Ok,
                FileUploads: fileUploads);
        }
        catch (SqliteException ex) when (ex.HasForeignKeyFailed()) //todo: test this
        {
            transaction.Rollback();

            Logger.Warning(ex,
                "Folder Foreign Key constraint failed while initiating FileUploads: '{FileUploadExternalIds}' in Folders: '{FolderIds}''",
                entities.Select(x => x.FileUploadExternalId),
                entities.Select(x => x.FolderId));

            return new Result(Code: ResultCode.FolderNotFound);
        }
        catch (Exception ex)
        {
            transaction.Rollback();

            Logger.Error(ex,
                "Error processing file uploads request for '{FileUploadExternalIds}'",
                entities.Select(x => x.FileUploadExternalId));

            throw;
        }
    }

    private static List<FileUpload> InsertFileUploads(
        WorkspaceContext workspace, 
        IUserIdentity userIdentity, 
        InsertEntity[] entities,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        var fileUploads = dbWriteContext
            .Cmd(
                sql: """
                    INSERT INTO fu_file_uploads(
                        fu_external_id,
                        fu_workspace_id,
                        fu_folder_id,
                        fu_s3_upload_id,
                        fu_owner_identity_type,
                        fu_owner_identity,
                        fu_file_name,
                        fu_file_extension,
                        fu_file_content_type,
                        fu_file_size_in_bytes,
                        fu_file_external_id,
                        fu_file_s3_key_secret_part,
                        fu_encryption_key_version,
                        fu_encryption_salt,
                        fu_encryption_nonce_prefix,
                        fu_is_completed,
                        fu_parent_file_id,
                        fu_file_metadata
                    )
                    SELECT
                        json_extract(value, '$.fileUploadExternalId'),
                        $workspaceId,
                        json_extract(value, '$.folderId'),
                        json_extract(value, '$.s3UploadId'),
                        $ownerIdentityType,
                        $ownerIdentity,
                        json_extract(value, '$.fileName'),
                        json_extract(value, '$.fileExtension'),
                        json_extract(value, '$.fileContentType'),
                        json_extract(value, '$.fileSizeInBytes'),
                        json_extract(value, '$.fileExternalId'),
                        json_extract(value, '$.s3KeySecretPart'),
                        json_extract(value, '$.encryptionKeyVersion'),
                        app_json_array_to_blob(json_extract(value, '$.encryptionSalt')),
                        app_json_array_to_blob(json_extract(value, '$.encryptionNoncePrefix')),
                        FALSE,
                        json_extract(value, '$.parentFileId'),
                        app_json_array_to_blob(json_extract(value, '$.fileMetadataBlob'))
                    FROM
                        json_each($fileUploads)
                    RETURNING 
                        fu_id,
                        fu_external_id
                    """,
                readRowFunc: reader => new FileUpload
                {
                    Id = reader.GetInt32(0),
                    ExternalId = reader.GetExtId<FileUploadExtId>(1)
                },
                transaction: transaction)
            .WithParameter("$workspaceId", workspace.Id)
            .WithParameter("$ownerIdentityType", userIdentity.IdentityType)
            .WithParameter("$ownerIdentity", userIdentity.Identity)
            .WithJsonParameter("$fileUploads", entities)
            .Execute();

        if (fileUploads.Count != entities.Length)
        {
            throw new InvalidOperationException(
                $"Something went wrong while inserting FileUploads. " +
                $"Expected uploads: '{string.Join(", ", entities.Select(x => x.FileUploadExternalId))}' " +
                $"Inserted uploads: '{string.Join(", ", fileUploads)}'");
        }

        return fileUploads;
    }

    public record Result(
        ResultCode Code,
        List<FileUpload>? FileUploads = default);

    public class FileUpload
    {
        public required int Id { get; init; }
        public required FileUploadExtId ExternalId { get; init; }
    }

    public enum ResultCode
    {
        Ok = 0,
        FolderNotFound
    }

    public class InsertEntity
    {
        public required string FileUploadExternalId { get; init; }
        public required string FileExternalId { get; init; }
        public required string S3UploadId { get; init; }
        public required string S3KeySecretPart { get; init; }
        public required int? FolderId { get; init; }
        public required string FileName { get; init; }
        public required string FileExtension { get; init; }
        public required string FileContentType { get; init; }
        public required long FileSizeInBytes { get; init; }
        public required byte? EncryptionKeyVersion { get; init; }
        public required byte[]? EncryptionSalt { get; init; }
        public required byte[]? EncryptionNoncePrefix { get; init; }
        public required int? ParentFileId { get; init; }
        public required byte[]? FileMetadataBlob { get; init; }
    }
}