using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Files.Download;
using PlikShare.Files.Id;
using PlikShare.Files.Preview.GetZipDetails.Contracts;
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
            var decodingResult = await ZipDecoder.ReadZipEntries(
                file: file,
                workspace: workspace,
                getFileEncryptionMode: f => workspace.GetFileEncryptionMode(
                    fileEncryptionMetadata: f.EncryptionMetadata,
                    workspaceEncryptionSession: workspaceEncryptionSession),
                cancellationToken: cancellationToken);

            if (decodingResult.Code == ZipDecoder.ZipDecodingResultCode.ZipFileBroken)
                return new Result { Code = ResultCode.ZipFileBroken };

            if (decodingResult.Code == ZipDecoder.ZipDecodingResultCode.Ok)
            {
                var response = ZipPreviewResponseBuilder.Build(
                    decodingResult.Entries!);

                return new Result
                {
                    Code = ResultCode.Ok,
                    Response = response
                };
            }

            throw new UnexpectedOperationResultException(
                operationName: nameof(ZipDecoder),
                resultValueStr: decodingResult.Code.ToString());
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
        public GetZipFileDetailsResponseDto? Response { get; init; }
    }


    public enum ResultCode
    {
        Ok = 0,
        FileNotFound,
        WrongFileExtension,
        ZipFileBroken
    }
}