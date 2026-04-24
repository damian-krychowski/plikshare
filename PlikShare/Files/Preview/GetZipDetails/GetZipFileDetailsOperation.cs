using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Files.Download;
using PlikShare.Files.Id;
using PlikShare.Files.Records;
using PlikShare.Storages.Exceptions;
using PlikShare.Storages.Zip;
using PlikShare.Workspaces.Cache;

namespace PlikShare.Files.Preview.GetZipDetails;

public class GetZipFileDetailsOperation(
    GetFileDetailsQuery getFileDetailsQuery)
{
    public async Task<Result> Execute(
        WorkspaceContext workspace,
        FileExtId fileExternalId,
        int? boxFolderId,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        CancellationToken cancellationToken)
    {
        var (isEmpty, file) = getFileDetailsQuery.Execute(
            workspaceId: workspace.Id,
            fileExternalId: fileExternalId,
            boxFolderId: boxFolderId,
            workspaceEncryptionSession: workspaceEncryptionSession);

        if (isEmpty)
            return new Result { Code = ResultCode.FileNotFound };

        if (file.Extension != ".zip")
            return new Result { Code = ResultCode.WrongFileExtension };
        
        try
        {
            var result = await ZipDecoder.ReadZipEntries(
                file: file.Resolve(
                    workspaceEncryptionSession: workspaceEncryptionSession,
                    storageClient: workspace.Storage),
                workspace: workspace,
                cancellationToken: cancellationToken);

            return result.Code switch
            {
                ZipDecoder.ZipDecodingResultCode.ZipFileBroken => new Result
                {
                    Code = ResultCode.ZipFileBroken
                },

                ZipDecoder.ZipDecodingResultCode.Ok => new Result
                {
                    Code = ResultCode.Ok,
                    Entries = result.Entries
                },

                _ => throw new UnexpectedOperationResultException(
                    operationName: nameof(ZipDecoder),
                    resultValueStr: result.Code.ToString())
            };
        }
        catch (FileNotFoundInStorageException)
        {
            return new Result
            {
                Code = ResultCode.FileNotFound
            };
        }
    }
    
    public class Result
    {
        public required ResultCode Code { get; init; }
        public List<ZipCdfhRecord>? Entries { get; init; }
    }


    public enum ResultCode
    {
        Ok = 0,
        FileNotFound,
        WrongFileExtension,
        ZipFileBroken
    }
}