using PlikShare.BulkDownload;
using PlikShare.Core.Clock;
using PlikShare.Core.Encryption;
using PlikShare.Core.UserIdentity;
using PlikShare.Files.PreSignedLinks;
using PlikShare.QuickShareExternalAccess.Contracts;
using PlikShare.QuickShares.Cache;
using PlikShare.QuickShares.EffectiveSet;

namespace PlikShare.QuickShareExternalAccess.GetBulkDownloadLink;

public class GenerateQuickShareBulkDownloadLinkOperation(
    GetQuickShareItemDbIdsQuery getItemDbIdsQuery,
    BulkDownloadDetailsQuery bulkDownloadDetailsQuery,
    PreSignedUrlsService preSignedUrlsService,
    IMasterDataEncryption masterDataEncryption,
    IClock clock)
{
    public Result Execute(
        QuickShareContext quickShare,
        IUserIdentity userIdentity,
        GetQuickShareBulkDownloadLinkRequestDto? request)
    {
        var dbIds = getItemDbIdsQuery.Execute(
            quickShareId: quickShare.Id);

        var hasNarrowing =
            (request?.SelectedFolderExternalIds?.Length ?? 0) > 0 ||
            (request?.SelectedFileExternalIds?.Length ?? 0) > 0 ||
            (request?.ExcludedFolderExternalIds?.Length ?? 0) > 0 ||
            (request?.ExcludedFileExternalIds?.Length ?? 0) > 0;

        int[] payloadSelectedFileIds;
        int[] payloadExcludedFileIds;
        int[] payloadSelectedFolderIds;
        int[] payloadExcludedFolderIds;

        if (!hasNarrowing)
        {
            payloadSelectedFileIds = dbIds.SelectedFileIds;
            payloadExcludedFileIds = dbIds.ExcludedFileIds;
            payloadSelectedFolderIds = dbIds.SelectedFolderIds;
            payloadExcludedFolderIds = dbIds.ExcludedFolderIds;
        }
        else
        {
            var details = bulkDownloadDetailsQuery.GetDetailsFromDb(
                selectedFileIds: dbIds.SelectedFileIds,
                excludedFileIds: dbIds.ExcludedFileIds,
                selectedFolderIds: dbIds.SelectedFolderIds,
                excludedFolderIds: dbIds.ExcludedFolderIds,
                workspace: quickShare.Workspace,
                workspaceEncryptionSession: null);

            var folderExtToInternal = details
                .FolderSubtree
                .GetDownloadFolders()
                .ToDictionary(f => f.ExternalId, f => f.Id);

            var fileExtToInternal = details
                .Files
                .ToDictionary(f => f.ExternalId, f => f.Id);

            // ResolveInternal drops external IDs that aren't in the share's effective set,
            // so the client cannot escape the share scope by sending foreign workspace ids.
            var selectedFolderInternal = ResolveInternal(
                request!.SelectedFolderExternalIds, folderExtToInternal);
            var excludedFolderInternal = ResolveInternal(
                request.ExcludedFolderExternalIds, folderExtToInternal);
            var selectedFileInternal = ResolveInternal(
                request.SelectedFileExternalIds, fileExtToInternal);
            var excludedFileInternal = ResolveInternal(
                request.ExcludedFileExternalIds, fileExtToInternal);

            if (selectedFolderInternal.Count == 0 && selectedFileInternal.Count == 0)
                return new Result(Code: ResultCode.EmptySelection);

            // Re-pass the share's own excludes so the zip still respects the creator's
            // exclusions even when the client narrows to a parent folder.
            payloadSelectedFolderIds = [.. selectedFolderInternal];
            payloadSelectedFileIds = [.. selectedFileInternal];
            payloadExcludedFolderIds = [.. excludedFolderInternal.Union(dbIds.ExcludedFolderIds)];
            payloadExcludedFileIds = [.. excludedFileInternal.Union(dbIds.ExcludedFileIds)];
        }

        var preSignedUrl = preSignedUrlsService.GeneratePreSignedBulkDownloadUrl(
            payload: new PreSignedUrlsService.BulkDownloadPayload
            {
                WorkspaceId = quickShare.Workspace.Id,
                SelectedFileIds = payloadSelectedFileIds,
                ExcludedFileIds = payloadExcludedFileIds,
                SelectedFolderIds = payloadSelectedFolderIds,
                ExcludedFolderIds = payloadExcludedFolderIds,
                PreSignedBy = new PreSignedUrlsService.PreSignedUrlOwner
                {
                    Identity = userIdentity.Identity,
                    IdentityType = userIdentity.IdentityType
                },
                ExpirationDate = clock.UtcNow.Add(TimeSpan.FromMinutes(1)),
                BoxLinkId = null,
                WorkspaceDeks = ((WorkspaceEncryptionSession?)null).ToWires(masterDataEncryption)
            });

        return new Result(
            Code: ResultCode.Ok,
            PreSignedUrl: preSignedUrl);
    }

    private static HashSet<int> ResolveInternal<TExtId>(
        TExtId[]? externalIds,
        Dictionary<TExtId, int> map) where TExtId : notnull
    {
        if (externalIds is null || externalIds.Length == 0)
            return [];

        var result = new HashSet<int>(externalIds.Length);

        foreach (var extId in externalIds)
        {
            if (map.TryGetValue(extId, out var internalId))
                result.Add(internalId);
        }

        return result;
    }

    public record Result(
        ResultCode Code,
        string? PreSignedUrl = default);

    public enum ResultCode
    {
        Ok = 0,
        EmptySelection
    }
}
