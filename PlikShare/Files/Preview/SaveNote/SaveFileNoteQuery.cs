using Microsoft.Data.Sqlite;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Core.UserIdentity;
using PlikShare.Files.Artifacts;
using PlikShare.Files.Id;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Files.Preview.SaveNote
{
    public class SaveFileNoteQuery(
        DbWriteQueue dbWriteQueue,
        IClock clock)
    {
        private static readonly Serilog.ILogger Logger = Log.ForContext<SaveFileNoteQuery>();

        public Task<ResultCode> Execute(
            WorkspaceContext workspace,
            FileExtId fileExternalId,
            IUserIdentity userIdentity,
            string contentJson,
            CancellationToken cancellationToken)
        {
            return dbWriteQueue.Execute(
                operationToEnqueue: context => ExecuteOperation(
                    dbWriteContext: context,
                    workspace: workspace,
                    fileExternalId: fileExternalId,
                    userIdentity: userIdentity,
                    contentJson: contentJson),
                cancellationToken: cancellationToken);
        }

        private ResultCode ExecuteOperation(
            DbWriteQueue.Context dbWriteContext,
            WorkspaceContext workspace,
            FileExtId fileExternalId,
            IUserIdentity userIdentity,
            string contentJson)
        {
            try
            {
                var externalId = FileArtifactExtId.NewId();
                var createdAt = clock.UtcNow;
                var uniquenessId = $"note_{fileExternalId}";

                var result = dbWriteContext
                    .OneRowCmd(
                        sql: @"
                            INSERT INTO fa_file_artifacts (
                                fa_external_id,
                                fa_workspace_id,
                                fa_file_id,
                                fa_type,
                                fa_content,
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
                                $noteContent,
                                $ownerIdentityType,
                                $ownerIdentity,
                                $createdAt,
                                $uniquenessId
                            FROM fi_files
                            WHERE 
                                fi_external_id = $fileExternalId
                                AND fi_workspace_id = $workspaceId
                            ON CONFLICT (fa_uniqueness_id)
                            DO UPDATE SET 
                                fa_content = EXCLUDED.fa_content,
                                fa_created_at = EXCLUDED.fa_created_at,
                                fa_owner_identity_type = EXCLUDED.fa_owner_identity_type,
                                fa_owner_identity = EXCLUDED.fa_owner_identity
                            RETURNING
                                fa_id
                        ",
                        readRowFunc: reader => reader.GetInt32(0))
                    .WithParameter("$externalId", externalId.Value)
                    .WithEnumParameter("$fileArtifactType", FileArtifactType.Note)
                    .WithBlobParameter("$noteContent", contentJson)
                    .WithParameter("$ownerIdentityType", userIdentity.IdentityType)
                    .WithParameter("$ownerIdentity", userIdentity.Identity)
                    .WithParameter("$createdAt", createdAt)
                    .WithParameter("$uniquenessId", uniquenessId)
                    .WithParameter("$fileExternalId", fileExternalId.Value)
                    .WithParameter("$workspaceId", workspace.Id)
                    .Execute();

                if (result.IsEmpty)
                {
                    Logger.Error(
                        "File not found while saving note. FileExternalId='{FileExternalId}', WorkspaceExternalId='{WorkspaceExternalId}'",
                        fileExternalId,
                        workspace.ExternalId);

                    return ResultCode.FileNotFound;
                }

                Logger.Information(
                    "Successfully saved note '{NoteExternalId} ({NoteId})' for file '{FileExternalId}' in workspace '{WorkspaceExternalId}'",
                    externalId,
                    result.Value,
                    fileExternalId,
                    workspace.ExternalId);

                return ResultCode.Ok;
            }
            catch (SqliteException ex) when (ex.HasForeignKeyFailed())
            {
                Logger.Error(ex,
                    "Foreign Key constraint failed while saving File Note. FileExternalId='{FileExternalId}', WorkspaceExternalId='{WorkspaceExternalId}'",
                    fileExternalId,
                    workspace.ExternalId);

                return ResultCode.FileNotFound;
            }
            catch (Exception e)
            {
                Logger.Error(e,
                    "Unexpected error while saving File Note. FileExternalId='{FileExternalId}', WorkspaceExternalId='{WorkspaceExternalId}'",
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
}
