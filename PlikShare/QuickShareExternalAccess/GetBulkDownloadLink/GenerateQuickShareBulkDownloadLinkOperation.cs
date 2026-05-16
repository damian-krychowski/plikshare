using PlikShare.Core.Clock;
using PlikShare.Core.Encryption;
using PlikShare.Core.UserIdentity;
using PlikShare.Files.PreSignedLinks;
using PlikShare.QuickShares.Cache;
using PlikShare.QuickShares.EffectiveSet;

namespace PlikShare.QuickShareExternalAccess.GetBulkDownloadLink;

public class GenerateQuickShareBulkDownloadLinkOperation(
    GetQuickShareItemDbIdsQuery getItemDbIdsQuery,
    PreSignedUrlsService preSignedUrlsService,
    IMasterDataEncryption masterDataEncryption,
    IClock clock)
{
    public string Execute(
        QuickShareContext quickShare,
        IUserIdentity userIdentity)
    {
        var dbIds = getItemDbIdsQuery.Execute(
            quickShareId: quickShare.Id);

        return preSignedUrlsService.GeneratePreSignedBulkDownloadUrl(
            payload: new PreSignedUrlsService.BulkDownloadPayload
            {
                WorkspaceId = quickShare.Workspace.Id,
                SelectedFileIds = dbIds.SelectedFileIds,
                ExcludedFileIds = dbIds.ExcludedFileIds,
                SelectedFolderIds = dbIds.SelectedFolderIds,
                ExcludedFolderIds = dbIds.ExcludedFolderIds,
                PreSignedBy = new PreSignedUrlsService.PreSignedUrlOwner
                {
                    Identity = userIdentity.Identity,
                    IdentityType = userIdentity.IdentityType
                },
                ExpirationDate = clock.UtcNow.Add(TimeSpan.FromMinutes(1)),
                BoxLinkId = null,
                WorkspaceDeks = ((WorkspaceEncryptionSession?)null).ToWires(masterDataEncryption)
            });
    }
}
