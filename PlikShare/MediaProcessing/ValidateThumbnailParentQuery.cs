using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;
using PlikShare.Workspaces.Cache;

namespace PlikShare.MediaProcessing;

/// <summary>
/// Cheap precondition check for the manual-thumbnail upload endpoint: does the parent file exist
/// in this workspace, and is its extension thumbnailable? Replaces the heavy lookup-then-validate
/// that <see cref="UploadFileThumbnailOperation"/> used to do inline — that operation now trusts
/// its caller and skips a per-variant DB round-trip plus ancestor-folder JSON aggregation.
///
/// Returns <see cref="ResultCode.NotFound"/> for a missing OR cross-workspace file (no oracle for
/// existence-vs-ownership — the endpoint surfaces both as 404, same as the previous behaviour).
/// </summary>
public class ValidateThumbnailParentQuery(PlikShareDb plikShareDb)
{
    public enum ResultCode
    {
        Ok = 0,
        NotFound,
        NotThumbnailable
    }

    public ResultCode Execute(
        WorkspaceContext workspace,
        FileExtId parentFileExternalId,
        WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
        using var connection = plikShareDb.OpenConnection();

        var row = connection
            .OneRowCmd(
                sql: """
                    SELECT
                        fi.fi_workspace_id,
                        fi.fi_extension
                    FROM fi_files AS fi
                    WHERE fi.fi_external_id = $externalId
                      AND fi.fi_deleted_at IS NULL
                    LIMIT 1
                """,
                readRowFunc: reader => new
                {
                    WorkspaceId = reader.GetInt32(0),
                    Extension = reader.DecodeEncryptableString(1, workspaceEncryptionSession)
                })
            .WithParameter("$externalId", parentFileExternalId.Value)
            .Execute();

        if (row.IsEmpty || row.Value.WorkspaceId != workspace.Id)
            return ResultCode.NotFound;

        return ContentTypeHelper.IsThumbnailable(row.Value.Extension)
            ? ResultCode.Ok
            : ResultCode.NotThumbnailable;
    }
}
