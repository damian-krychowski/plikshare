using System.Security.Cryptography;
using System.Text;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Core.UserIdentity;
using PlikShare.Files.Artifacts;
using PlikShare.Files.Id;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Files.Preview.Comment.EditComment;

public class UpdateFileCommentQuery(DbWriteQueue dbWriteQueue)
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<UpdateFileCommentQuery>();

    public Task<ResultCode> Execute(
        WorkspaceContext workspace,
        FileExtId fileExternalId,
        FileArtifactExtId commentExternalId,
        IUserIdentity userIdentity,
        EncryptableMetadata updatedCommentContent,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                workspace: workspace,
                fileExternalId: fileExternalId,
                commentExternalId: commentExternalId,
                userIdentity: userIdentity,
                updatedCommentContent: updatedCommentContent),
            cancellationToken: cancellationToken);
    }

    private ResultCode ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        WorkspaceContext workspace,
        FileExtId fileExternalId,
        FileArtifactExtId commentExternalId,
        IUserIdentity userIdentity,
        EncryptableMetadata updatedCommentContent)
    {
        try
        {
            // First, verify file exists and get its internal ID
            var fileResult = dbWriteContext
                .OneRowCmd(
                    sql: @"
                        SELECT fi_id
                        FROM fi_files
                        WHERE fi_external_id = $fileExternalId
                        AND fi_workspace_id = $workspaceId",
                    readRowFunc: reader => reader.GetInt32(0))
                .WithParameter("$fileExternalId", fileExternalId.Value)
                .WithParameter("$workspaceId", workspace.Id)
                .Execute();

            if (fileResult.IsEmpty)
            {
                Logger.Error(
                    "File not found while updating comment '{CommentExternalId}'. FileExternalId='{FileExternalId}', WorkspaceExternalId='{WorkspaceExternalId}'",
                    commentExternalId,
                    fileExternalId,
                    workspace.ExternalId);

                return ResultCode.FileNotFound;
            }

            var contentHash = SHA256.HashData(Encoding.UTF8.GetBytes(updatedCommentContent.Value));

            // Then update the comment, checking ownership
            var result = dbWriteContext
                .OneRowCmd(
                    sql: @"
                        UPDATE fa_file_artifacts
                        SET fa_content = $content,
                            fa_content_hash = $contentHash
                        WHERE
                            fa_external_id = $commentExternalId
                            AND fa_file_id = $fileId
                            AND fa_type = $fileArtifactType
                            AND fa_owner_identity_type = $ownerIdentityType
                            AND fa_owner_identity = $ownerIdentity
                        RETURNING fa_id",
                    readRowFunc: reader => reader.GetInt32(0))
                .WithParameter("$commentExternalId", commentExternalId.Value)
                .WithParameter("$fileId", fileResult.Value)
                .WithEnumParameter("$fileArtifactType", FileArtifactType.Comment)
                .WithEncryptableBlobParameter("$content", updatedCommentContent)
                .WithParameter("$contentHash", contentHash)
                .WithParameter("$ownerIdentityType", userIdentity.IdentityType)
                .WithParameter("$ownerIdentity", userIdentity.Identity)
                .Execute();

            if (result.IsEmpty)
            {
                Logger.Error(
                    "Failed to update comment. Comment not found or user is not the owner. FileExternalId='{FileExternalId}', CommentExternalId='{CommentExternalId}', WorkspaceExternalId='{WorkspaceExternalId}'",
                    fileExternalId,
                    commentExternalId,
                    workspace.ExternalId);

                return ResultCode.CommentNotFoundOrNotOwner;
            }

            Logger.Information(
                "Successfully updated comment '{CommentExternalId} ({CommentId})' for file '{FileExternalId}' in workspace '{WorkspaceExternalId}'",
                commentExternalId,
                result.Value,
                fileExternalId,
                workspace.ExternalId);

            return ResultCode.Ok;
        }
        catch (Exception e)
        {
            Logger.Error(e,
                "Unexpected error while updating File Comment. FileExternalId='{FileExternalId}', CommentExternalId='{CommentExternalId}', WorkspaceExternalId='{WorkspaceExternalId}'",
                fileExternalId,
                commentExternalId,
                workspace.ExternalId);

            throw;
        }
    }

    public enum ResultCode
    {
        Ok = 0,
        FileNotFound,
        CommentNotFoundOrNotOwner
    }
}

