using PlikShare.Core.Utils;
using PlikShare.Files.Id;
using PlikShare.Files.Preview.GetZipDetails;
using PlikShare.QuickShareExternalAccess.EffectiveSet;
using PlikShare.QuickShares.Cache;

namespace PlikShare.QuickShareExternalAccess.GetZipFileDetails;

public class GenerateQuickShareZipFileDetailsOperation(
    IsFileInQuickShareQuery isFileInQuickShareQuery,
    GetZipFileDetailsOperation getZipFileDetailsOperation)
{
    public async Task<Result> Execute(
        QuickShareContext quickShare,
        FileExtId fileExternalId,
        CancellationToken cancellationToken)
    {
        if (!isFileInQuickShareQuery.Execute(quickShare, fileExternalId))
            return new Result { Code = ResultCode.FileNotInShare };

        var result = await getZipFileDetailsOperation.Execute(
            workspace: quickShare.Workspace,
            fileExternalId: fileExternalId,
            boxFolderId: null,
            workspaceEncryptionSession: null,
            cancellationToken: cancellationToken);

        return result.Code switch
        {
            GetZipFileDetailsOperation.ResultCode.Ok => new Result
            {
                Code = ResultCode.Ok,
                Response = result.Response
            },

            GetZipFileDetailsOperation.ResultCode.FileNotFound => new Result
            {
                Code = ResultCode.FileNotFound
            },

            GetZipFileDetailsOperation.ResultCode.WrongFileExtension => new Result
            {
                Code = ResultCode.WrongFileExtension
            },

            GetZipFileDetailsOperation.ResultCode.ZipFileBroken => new Result
            {
                Code = ResultCode.ZipFileBroken
            },

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(GetZipFileDetailsOperation),
                resultValueStr: result.Code.ToString())
        };
    }

    public class Result
    {
        public required ResultCode Code { get; init; }
        public Files.Preview.GetZipDetails.Contracts.GetZipFileDetailsResponseDto? Response { get; init; }
    }

    public enum ResultCode
    {
        Ok = 0,
        FileNotFound,
        FileNotInShare,
        WrongFileExtension,
        ZipFileBroken
    }
}
