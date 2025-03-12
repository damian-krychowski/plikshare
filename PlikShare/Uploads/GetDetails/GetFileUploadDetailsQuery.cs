using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Core.UserIdentity;
using PlikShare.Uploads.Algorithm;
using PlikShare.Uploads.Id;
using PlikShare.Workspaces.Cache;

namespace PlikShare.Uploads.GetDetails;

public class GetFileUploadDetailsQuery(
    PlikShareDb plikShareDb)
{
    public Result Execute(
        FileUploadExtId uploadExternalId,
        WorkspaceContext workspace,
        IUserIdentity userIdentity)
    {
        using var connection = plikShareDb.OpenConnection();

        var uploadDetails = connection
            .OneRowCmd(
                sql: @"
                    SELECT 
                        fu_id,
                        (
					        SELECT json_group_array(fup_part_number)
					        FROM fup_file_upload_parts
					        WHERE fup_file_upload_id = fu_id
					        ORDER BY fup_part_number
					    ) AS fu_already_uploaded_part_numbers,
                        fu_file_size_in_bytes
                    FROM fu_file_uploads
                    WHERE
                        fu_external_id = $fileUploadExternalId
                        AND fu_workspace_id = $workspaceId
		  				AND fu_owner_identity_type = $ownerIdentityType
		  				AND fu_owner_identity = $ownerIdentity
                        AND fu_is_completed = FALSE
                    LIMIT 1
                ",
                readRowFunc: reader =>
                {
                    var fileSizeInBytes = reader.GetInt64(2);

                    var (algorithm, partsCount) = workspace
                        .Storage
                        .ResolveUploadAlgorithm(fileSizeInBytes);

                    return new UploadDetails
                    {
                        FileUploadId = reader.GetInt32(0),
                        AlreadyUploadedPartNumbers =
                            reader.GetFromJson<List<int>>(1),
                        Algorithm = algorithm,
                        ExpectedPartsCount = partsCount
                    };
                })
            .WithParameter("$fileUploadExternalId", uploadExternalId.Value)
            .WithParameter("$workspaceId", workspace.Id)
            .WithParameter("$ownerIdentityType", userIdentity.IdentityType)
            .WithParameter("$ownerIdentity", userIdentity.Identity)
            .Execute();

        if (uploadDetails.IsEmpty)
            return new Result(Code: ResultCode.NotFound);

        return new Result(
            Code: ResultCode.Ok,
            Details: uploadDetails.Value);
    }

    public enum ResultCode
    {
        Ok = 0,
        NotFound
    }

    public record Result(
        ResultCode Code,
        UploadDetails? Details = default);

    public class UploadDetails
    {
        public required int FileUploadId { get; init; }
        public required List<int> AlreadyUploadedPartNumbers { get; init; }
        public required UploadAlgorithm Algorithm { get; init; }
        public required int ExpectedPartsCount { get; init; }
    }
}