using PlikShare.Core.UserIdentity;
using PlikShare.Uploads.Cache;
using PlikShare.Uploads.Id;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Uploads.FilePartUpload.Complete;

public class CompleteFilePartUploadQuery(
    InsertFileUploadPartQuery insertFileUploadPartQuery,
    FileUploadCache fileUploadCache)
{
    public async ValueTask<ResultCode> Execute(
        WorkspaceContext workspace,
        int partNumber,
        FileUploadExtId fileUploadExternalId,
        string eTag,
        IUserIdentity userIdentity,
        CancellationToken cancellationToken)
    {
        var fileUpload = await fileUploadCache.GetFileUpload(
            uploadExternalId: fileUploadExternalId,
            cancellationToken: cancellationToken);
        
        if (fileUpload is null || !fileUpload.HasUserRight(workspace.Id, userIdentity))
        {
            Log.Warning("Could not complete FileUpload '{FileUploadExternalId}' part '{FileUploadPartNumber}' because FileUpload was not found",
                fileUploadExternalId,
                partNumber);

            return ResultCode.FileUploadNotFound;
        }

        var result = await insertFileUploadPartQuery.Execute(
            fileUploadId: fileUpload.Id,
            partNumber: partNumber,
            eTag: eTag,
            cancellationToken: cancellationToken);

        if (result.Code == InsertFileUploadPartQuery.ResultCode.FileUploadNotFound)
            return ResultCode.FileUploadNotFound;

        Log.Debug("FileUpload '{FileUploadExternalId}' part '#{FileUploadPartNumber}' was completed.",
            fileUploadExternalId,
            partNumber);

        return ResultCode.Ok;
    }

    public enum ResultCode
    {
        Ok = 0,
        FileUploadNotFound
    }
}