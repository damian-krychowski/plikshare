using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Core.UserIdentity;
using PlikShare.Files.Id;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Files.Preview.Comment.DeleteComment;

public class DeleteFileCommentQuery(DbWriteQueue dbWriteQueue)
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<DeleteFileCommentQuery>();
    
    public Task<ResultCode> Execute(
        WorkspaceContext workspace,
        FileExtId fileExternalId,
        FileArtifactExtId commentExternalId,
        IUserIdentity userIdentity,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                workspace: workspace,
                fileExternalId: fileExternalId,
                commentExternalId: commentExternalId,
                userIdentity: userIdentity,
                isAdmin: isAdmin),
            cancellationToken: cancellationToken);
    }

    private ResultCode ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        WorkspaceContext workspace,
        FileExtId fileExternalId,
        FileArtifactExtId commentExternalId,
        IUserIdentity userIdentity,
        bool isAdmin)
    {
        Logger.Debug(
           "Starting to delete comment '{CommentExternalId}' for file '{FileExternalId}' in workspace '{WorkspaceExternalId}'",
           commentExternalId,
           fileExternalId,
           workspace.ExternalId);
        
        try
        {
            var fileResult = dbWriteContext
                .OneRowCmd(
                    sql: @"
                        SELECT fi_id
                        FROM fi_files
                        WHERE 
                            fi_external_id = $fileExternalId
                            AND fi_workspace_id = $workspaceId
                    ",
                    readRowFunc: reader => reader.GetInt32(0))
                .WithParameter("$fileExternalId", fileExternalId.Value)
                .WithParameter("$workspaceId", workspace.Id)
                .Execute();

            if (fileResult.IsEmpty)
            {
                Logger.Warning(
                    "File not found while deleting comment. FileExternalId='{FileExternalId}', WorkspaceExternalId='{WorkspaceExternalId}'",
                    fileExternalId,
                    workspace.ExternalId);
                return ResultCode.FileNotFound;
            }

            Logger.Debug(
                "Found file ID {FileId} for external ID '{FileExternalId}'",
                fileResult.Value,
                fileExternalId);

            var result = dbWriteContext
                .OneRowCmd(
                    sql: @"
                        DELETE FROM fa_file_artifacts
                        WHERE
                            fa_external_id = $commentExternalId
                            AND fa_file_id = $fileId
                            AND (
                                $isAdmin OR (
                                    fa_owner_identity_type = $ownerIdentityType
                                    AND fa_owner_identity = $ownerIdentity
                                )
                            )
                        RETURNING
                            fa_id
                    ",
                    readRowFunc: reader => reader.GetInt32(0))
                .WithParameter("$commentExternalId", commentExternalId.Value)
                .WithParameter("$fileId", fileResult.Value)
                .WithParameter("$ownerIdentityType", userIdentity.IdentityType)
                .WithParameter("$ownerIdentity", userIdentity.Identity)
                .WithParameter("$isAdmin", isAdmin)
                .Execute();

            if (result.IsEmpty)
            {
                Logger.Warning(
                    "Comment not found. CommentExternalId='{CommentExternalId}', FileExternalId='{FileExternalId}', WorkspaceExternalId='{WorkspaceExternalId}'",
                    commentExternalId,
                    fileExternalId,
                    workspace.ExternalId);
                return ResultCode.CommentNotFoundOrNotOwner;
            }

            Logger.Information(
                "Successfully deleted comment '{CommentExternalId}' (ID: {CommentId}) from file '{FileExternalId}' in workspace '{WorkspaceExternalId}'",
                commentExternalId,
                result.Value,
                fileExternalId,
                workspace.ExternalId);

            return ResultCode.Ok;
        }
        catch (Exception ex)
        {
            Logger.Error(ex,
                "Unexpected error while deleting comment '{CommentExternalId}' from file '{FileExternalId}' in workspace '{WorkspaceExternalId}'",
                commentExternalId,
                fileExternalId,
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