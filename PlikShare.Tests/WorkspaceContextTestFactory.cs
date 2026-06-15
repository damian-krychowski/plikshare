using System.Security.Cryptography;
using PlikShare.Core.Encryption;
using PlikShare.MediaProcessing;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.HardDrive;
using PlikShare.Storages.HardDrive.StorageClient;
using PlikShare.Storages.Id;
using PlikShare.Trash;
using PlikShare.Users.Cache;
using PlikShare.Users.Entities;
using PlikShare.Users.Id;
using PlikShare.Users.StorageAccess;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;

namespace PlikShare.Tests;

/// <summary>
/// Builds <see cref="WorkspaceContext"/> instances for unit tests that exercise the metadata
/// encrypt/decrypt path. The session-level convenience that used to encode metadata moved to
/// <see cref="WorkspaceContextExtensions.EncodeMetadata"/> /
/// <see cref="WorkspaceContextExtensions.ToEncryptableMetadata"/>, so tests now need a real
/// <see cref="WorkspaceContext"/> whose <see cref="WorkspaceContext.Storage"/> reports the
/// right <see cref="StorageEncryption"/>. Those methods only read <c>Storage.Encryption</c>
/// (and <c>Storage.ExternalId</c> for error text), so the surrounding workspace fields are
/// filled with inert placeholders — the per-call key material always comes from the
/// <see cref="WorkspaceEncryptionSession"/> argument, never the workspace.
/// </summary>
internal static class WorkspaceContextTestFactory
{
    /// <summary>
    /// A full-encrypted workspace: <c>EncodeMetadata</c> / <c>ToEncryptableMetadata</c> take
    /// the single-chain-step branch keyed off the session DEK. The workspace's own DEK version
    /// is irrelevant — the envelope's key version is taken from the session's latest DEK — so
    /// a single shared instance serves every encrypted-path test.
    /// </summary>
    public static WorkspaceContext CreateEncrypted()
    {
        return Create(
            new FullStorageEncryption(
                new StorageFullEncryptionDetails(
                    RecoveryVerifyHash: [],
                    LatestStorageDekVersion: 0)));
    }

    /// <summary>
    /// An unencrypted workspace, used for null-session passthrough cases where
    /// <c>EncodeMetadata</c> / <c>ToEncryptableMetadata</c> hand the value back verbatim.
    /// </summary>
    public static WorkspaceContext CreatePlain()
    {
        return Create(
            NoStorageEncryption.Instance);
    }

    public static WorkspaceContext Create(StorageEncryption encryption)
    {
        var storage = new HardDriveStorageClient(
            details: new HardDriveDetailsEntity(
                VolumePath: "",
                FolderPath: "",
                FullPath: ""),
            storageId: 1,
            externalId: StorageExtId.NewId(),
            name: "test-storage",
            encryption: encryption,
            defaultTrashPolicy: new TrashPolicy(
                Enabled: false,
                RetentionDays: null));

        return new WorkspaceContext
        {
            Id = 42,
            ExternalId = WorkspaceExtId.NewId(),
            Name = "test-workspace",
            MaxSizeInBytes = null,
            MaxTeamMembers = null,
            BucketName = "test-bucket",
            IsBucketCreated = true,
            IsBeingDeleted = false,
            Owner = CreateOwner(),
            Storage = storage,
            EncryptionMetadata = encryption is FullStorageEncryption
                ? new WorkspaceEncryptionMetadata { Salt = RandomNumberGenerator.GetBytes(32) }
                : null,
            Integrations = new WorkspaceIntegrations
            {
                Textract = null,
                ChatGpt = []
            },
            TrashPolicy = new TrashPolicy(
                Enabled: false,
                RetentionDays: null),
            MediaProcessingPolicy = null
        };
    }

    private static UserContext CreateOwner()
    {
        return new UserContext
        {
            Status = UserStatus.Registered,
            Id = 1,
            ExternalId = UserExtId.NewId(),
            Email = new Email("owner@test.local"),
            IsEmailConfirmed = true,
            Stamps = new UserSecurityStamps
            {
                Security = "",
                Concurrency = ""
            },
            Roles = new UserRoles
            {
                IsAppOwner = true,
                IsAdmin = false
            },
            Permissions = new UserPermissions
            {
                CanAddWorkspace = true,
                CanManageGeneralSettings = false,
                CanManageUsers = false,
                CanManageStorages = false,
                CanManageEmailProviders = false,
                CanManageAuth = false,
                CanManageIntegrations = false,
                CanManageAuditLog = false,
                CanManageAgents = false
            },
            HasPassword = true,
            MaxWorkspaceNumber = null,
            DefaultMaxWorkspaceSizeInBytes = null,
            DefaultMaxWorkspaceTeamMembers = null,
            EncryptionMetadata = null,
            WrappedStorageDeks = [],
            WrappedWorkspaceDeks = [],
            StorageAccess = new UserStorageAccess
            {
                Mode = UserStorageAccessMode.All,
                StorageIds = []
            }
        };
    }
}
