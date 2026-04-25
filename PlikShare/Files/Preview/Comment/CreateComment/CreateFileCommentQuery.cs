using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Files.Artifacts;
using PlikShare.Files.Id;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Files.Preview.Comment.CreateComment;

public class CreateFileCommentQuery(DbWriteQueue dbWriteQueue, IClock clock)
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<CreateFileCommentQuery>();

    public Task<ResultCode> Execute(
        WorkspaceContext workspace,
        FileExtId fileExternalId,
        IUserIdentity userIdentity,
        FileArtifactExtId commentExternalId,
        EncryptableMetadata commentContent,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                workspace: workspace,
                fileExternalId: fileExternalId,
                userIdentity: userIdentity,
                commentExternalId: commentExternalId,
                commentContent: commentContent),
            cancellationToken: cancellationToken);
    }

    private ResultCode ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        WorkspaceContext workspace,
        FileExtId fileExternalId,
        IUserIdentity userIdentity,
        FileArtifactExtId commentExternalId,
        EncryptableMetadata commentContent)
    {
        try
        {
            var createdAt = clock.UtcNow;
            var contentHash = SHA256.HashData(Encoding.UTF8.GetBytes(commentContent.Value));

            var result = dbWriteContext
                .OneRowCmd(
                    sql: @"
                            INSERT INTO fa_file_artifacts (
                                fa_external_id,
                                fa_workspace_id,
                                fa_file_id,
                                fa_type,
                                fa_content,
                                fa_content_hash,
                                fa_owner_identity_type,
                                fa_owner_identity,
                                fa_created_at,
                                fa_uniqueness_id
                            )
                            SELECT
                                $externalId,
                                fi_workspace_id,
                                fi_id,
                                $fileArtifactType,
                                $content,
                                $contentHash,
                                $ownerIdentityType,
                                $ownerIdentity,
                                $createdAt,
                                NULL
                            FROM fi_files
                            WHERE
                                fi_external_id = $fileExternalId
                                AND fi_workspace_id = $workspaceId
                            RETURNING
                                fa_id
                        ",
                    readRowFunc: reader => reader.GetInt32(0))
                .WithParameter("$externalId", commentExternalId.Value)
                .WithEnumParameter("$fileArtifactType", FileArtifactType.Comment)
                .WithEncryptableBlobParameter("$content", commentContent)
                .WithParameter("$contentHash", contentHash)
                .WithParameter("$ownerIdentityType", userIdentity.IdentityType)
                .WithParameter("$ownerIdentity", userIdentity.Identity)
                .WithParameter("$createdAt", createdAt)
                .WithParameter("$fileExternalId", fileExternalId.Value)
                .WithParameter("$workspaceId", workspace.Id)
                .Execute();

            if (result.IsEmpty)
            {
                Logger.Error(
                    "File not found while saving comment. FileExternalId='{FileExternalId}', WorkspaceExternalId='{WorkspaceExternalId}'",
                    fileExternalId,
                    workspace.ExternalId);

                return ResultCode.FileNotFound;
            }

            Logger.Information(
                "Successfully saved comment '{CommentExternalId} ({CommentId})' for file '{FileExternalId}' in workspace '{WorkspaceExternalId}'",
                commentExternalId,
                result.Value,
                fileExternalId,
                workspace.ExternalId);

            return ResultCode.Ok;
        }
        catch (SqliteException ex) when (ex.HasForeignKeyFailed())
        {
            Logger.Error(ex,
                "Foreign Key constraint failed while saving File Comment. FileExternalId='{FileExternalId}', WorkspaceExternalId='{WorkspaceExternalId}'",
                fileExternalId,
                workspace.ExternalId);

            return ResultCode.FileNotFound;
        }
        catch (Exception e)
        {
            Logger.Error(e,
                "Unexpected error while saving File Comment. FileExternalId='{FileExternalId}', WorkspaceExternalId='{WorkspaceExternalId}'",
                fileExternalId,
                workspace.ExternalId);

            throw;
        }
    }

    public enum ResultCode
    {
        Ok = 0,
        FileNotFound
    }
}