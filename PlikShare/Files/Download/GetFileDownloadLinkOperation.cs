using PlikShare.Core.Clock;
using PlikShare.Core.Encryption;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;
using PlikShare.Files.PreSignedLinks;
using PlikShare.Storages;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.HardDrive.StorageClient;
using PlikShare.Workspaces.Cache;

namespace PlikShare.Files.Download;

public class GetFileDownloadLinkOperation(
    GetFileDetailsQuery getFileDetailsQuery,
    PreSignedUrlsService preSignedUrlsService,
    IMasterDataEncryption masterDataEncryption,
    IClock clock)
{
    public async ValueTask<Result> Execute(
        WorkspaceContext workspace,
        FileExtId fileExternalId,
        ContentDispositionType contentDisposition,
        int? boxFolderId,
        int? boxLinkId,
        IUserIdentity userIdentity,
        bool enforceInternalPassThrough,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        DateTimeOffset? expiresAt,
        CancellationToken cancellationToken)
    {
        var fileQueryResult = getFileDetailsQuery.Execute(
            workspaceId: workspace.Id,
            fileExternalId: fileExternalId,
            boxFolderId: boxFolderId,
            workspaceEncryptionSession: workspaceEncryptionSession);

        if (fileQueryResult.IsEmpty)
            return new Result(Code: ResultCode.FileNotFound);

        var key = new FileKey
        {
            FileExternalId = fileExternalId,
            KeySecretPart = fileQueryResult.Value.KeySecretPart
        };

        var preSignedUrl = workspace.Storage switch
        {
            HardDriveStorageClient => HandleHardDrivePreSignedDownloadFileLink(
                key: key,
                contentDisposition: contentDisposition,
                boxLinkId: boxLinkId,
                userIdentity: userIdentity,
                workspaceEncryptionSession: workspaceEncryptionSession,
                expiresAt: expiresAt),

            IObjectStorageClient objectStorageClient => await HandleObjectStoragePreSignedDownloadFileLink(
                objectStorageClient: objectStorageClient,
                bucketName: workspace.BucketName,
                key: key,
                contentType: fileQueryResult.Value.ContentType,
                fileName: fileQueryResult.Value.Name + fileQueryResult.Value.Extension,
                contentDisposition: contentDisposition,
                boxLinkId: boxLinkId,
                userIdentity: userIdentity,
                enforceInternalPassThrough: enforceInternalPassThrough,
                workspaceEncryptionSession: workspaceEncryptionSession,
                expiresAt: expiresAt,
                cancellationToken: cancellationToken),

            _ => throw new ArgumentOutOfRangeException(nameof(workspace.Storage))
        };
        
        return new Result(
            Code: ResultCode.Ok,
            DownloadPreSignedUrl: preSignedUrl);
    }

    private string HandleHardDrivePreSignedDownloadFileLink(
        FileKey key,
        ContentDispositionType contentDisposition,
        int? boxLinkId,
        IUserIdentity userIdentity,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        DateTimeOffset? expiresAt)
    {
        return preSignedUrlsService.GeneratePreSignedDownloadUrl(
            payload: new PreSignedUrlsService.DownloadPayload
            {
                FileExternalId = key.FileExternalId,
                PreSignedBy = new PreSignedUrlsService.PreSignedUrlOwner
                {
                    Identity = userIdentity.Identity,
                    IdentityType = userIdentity.IdentityType
                },
                ContentDisposition = contentDisposition,
                ExpirationDate = expiresAt ?? clock.UtcNow.Add(TimeSpan.FromDays(1)),
                BoxLinkId = boxLinkId,
                WorkspaceDeks = workspaceEncryptionSession.ToWires(masterDataEncryption)
            });
    }

    public async ValueTask<string> HandleObjectStoragePreSignedDownloadFileLink(
        IObjectStorageClient objectStorageClient,
        string bucketName,
        FileKey key,
        string contentType,
        string fileName,
        ContentDispositionType contentDisposition,
        int? boxLinkId,
        IUserIdentity userIdentity,
        bool enforceInternalPassThrough,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        DateTimeOffset? expiresAt,
        CancellationToken cancellationToken = default)
    {
        if (objectStorageClient.Encryption is ManagedStorageEncryption or FullStorageEncryption || enforceInternalPassThrough)
        {
            return preSignedUrlsService.GeneratePreSignedDownloadUrl(
                new PreSignedUrlsService.DownloadPayload
                {
                    FileExternalId = key.FileExternalId,
                    PreSignedBy = new PreSignedUrlsService.PreSignedUrlOwner
                    {
                        Identity = userIdentity.Identity,
                        IdentityType = userIdentity.IdentityType
                    },
                    ContentDisposition = contentDisposition,
                    ExpirationDate = expiresAt ?? clock.UtcNow.Add(TimeSpan.FromDays(1)),
                    BoxLinkId = boxLinkId,
                    WorkspaceDeks = workspaceEncryptionSession.ToWires(masterDataEncryption)
                });
        }

        if (objectStorageClient.Encryption is NoStorageEncryption)
        {
            return await objectStorageClient.GetPreSignedDownloadFileLink(
                bucketName: bucketName,
                key: key,
                contentType: contentType,
                contentDisposition: contentDisposition,
                fileName: fileName,
                expiresAt: expiresAt ?? clock.UtcNow.AddHours(3));
        }

        throw new NotImplementedException($"Unknown encryption type: '{objectStorageClient.Encryption.GetType()}'");
    }

    public record Result(
        ResultCode Code,
        string? DownloadPreSignedUrl = null);
    
    public enum ResultCode
    {
        Ok = 0,
        FileNotFound
    }
}