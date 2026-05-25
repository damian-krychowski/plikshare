using PlikShare.Core.Utils;
using PlikShare.QuickShares;

namespace PlikShare.AuditLog.Details;

public static partial class Audit
{
    public static class QuickShare
    {
        public class Created
        {
            public required WorkspaceRef Workspace { get; init; }
            public required QuickShareRef QuickShare { get; init; }
            public required QuickShareMode Mode { get; init; }
            public required bool AllowIndividualFileDownload { get; init; }
            public required bool HasPassword { get; init; }
            public required int? MaxDownloads { get; init; }
            public required DateTimeOffset? ExpiresAt { get; init; }
            public required List<FileRef> SelectedFiles { get; init; }
            public required List<FolderRef> SelectedFolders { get; init; }
            public required List<FileRef> ExcludedFiles { get; init; }
            public required List<FolderRef> ExcludedFolders { get; init; }
        }

        public class Deleted
        {
            public required WorkspaceRef Workspace { get; init; }
            public required QuickShareRef QuickShare { get; init; }
        }

        public class NameUpdated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required QuickShareRef QuickShare { get; init; }
        }

        public class SlugUpdated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required QuickShareRef QuickShare { get; init; }
            public required string OldSlug { get; init; }
            public required string NewSlug { get; init; }
        }

        public class ExpirationUpdated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required QuickShareRef QuickShare { get; init; }
            public required DateTimeOffset? ExpiresAt { get; init; }
        }

        public class PasswordUpdated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required QuickShareRef QuickShare { get; init; }
            public required bool IsSet { get; init; }
        }

        public class MaxDownloadsUpdated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required QuickShareRef QuickShare { get; init; }
            public required int? MaxDownloads { get; init; }
        }

        public class ModeUpdated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required QuickShareRef QuickShare { get; init; }
            public required QuickShareMode Mode { get; init; }
            public required bool AllowIndividualFileDownload { get; init; }
        }

        public class ItemsUpdated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required QuickShareRef QuickShare { get; init; }
            public required List<FileRef> SelectedFiles { get; init; }
            public required List<FolderRef> SelectedFolders { get; init; }
            public required List<FileRef> ExcludedFiles { get; init; }
            public required List<FolderRef> ExcludedFolders { get; init; }
        }

        public class Unlocked
        {
            public required WorkspaceRef Workspace { get; init; }
            public required QuickShareRef QuickShare { get; init; }
        }

        public class UnlockFailed
        {
            public required WorkspaceRef Workspace { get; init; }
            public required QuickShareRef QuickShare { get; init; }
        }

        public class BulkDownloadLinkGenerated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required QuickShareRef QuickShare { get; init; }
            public required int DownloadsCountAfter { get; init; }
        }

        public class FileDownloadLinkGenerated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required QuickShareRef QuickShare { get; init; }
            public required FileRef File { get; init; }
            public required int DownloadsCountAfter { get; init; }
        }

        public class FilePreviewLinkGenerated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required QuickShareRef QuickShare { get; init; }
            public required FileRef File { get; init; }
        }

        public class DownloadLimitReached
        {
            public required WorkspaceRef Workspace { get; init; }
            public required QuickShareRef QuickShare { get; init; }
        }

        public static AuditLogEntry CreatedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            QuickShareRef quickShare,
            QuickShareMode mode,
            bool allowIndividualFileDownload,
            bool hasPassword,
            int? maxDownloads,
            DateTimeOffset? expiresAt,
            List<FileRef> selectedFiles,
            List<FolderRef> selectedFolders,
            List<FileRef> excludedFiles,
            List<FolderRef> excludedFolders) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.QuickShare,
            EventType = AuditLogEventTypes.QuickShare.Created,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            DetailsJson = Json.Serialize(new Created
            {
                Workspace = workspace,
                QuickShare = quickShare,
                Mode = mode,
                AllowIndividualFileDownload = allowIndividualFileDownload,
                HasPassword = hasPassword,
                MaxDownloads = maxDownloads,
                ExpiresAt = expiresAt,
                SelectedFiles = selectedFiles,
                SelectedFolders = selectedFolders,
                ExcludedFiles = excludedFiles,
                ExcludedFolders = excludedFolders
            })
        };

        public static AuditLogEntry DeletedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            QuickShareRef quickShare) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.QuickShare,
            EventType = AuditLogEventTypes.QuickShare.Deleted,
            Severity = AuditLogSeverities.Warning,
            WorkspaceExternalId = workspace.ExternalId.Value,
            DetailsJson = Json.Serialize(new Deleted
            {
                Workspace = workspace,
                QuickShare = quickShare
            })
        };

        public static AuditLogEntry NameUpdatedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            QuickShareRef quickShare) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.QuickShare,
            EventType = AuditLogEventTypes.QuickShare.NameUpdated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            DetailsJson = Json.Serialize(new NameUpdated
            {
                Workspace = workspace,
                QuickShare = quickShare
            })
        };

        public static AuditLogEntry SlugUpdatedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            QuickShareRef quickShare,
            string oldSlug,
            string newSlug) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.QuickShare,
            EventType = AuditLogEventTypes.QuickShare.SlugUpdated,
            Severity = AuditLogSeverities.Warning,
            WorkspaceExternalId = workspace.ExternalId.Value,
            DetailsJson = Json.Serialize(new SlugUpdated
            {
                Workspace = workspace,
                QuickShare = quickShare,
                OldSlug = oldSlug,
                NewSlug = newSlug
            })
        };

        public static AuditLogEntry ExpirationUpdatedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            QuickShareRef quickShare,
            DateTimeOffset? expiresAt) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.QuickShare,
            EventType = AuditLogEventTypes.QuickShare.ExpirationUpdated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            DetailsJson = Json.Serialize(new ExpirationUpdated
            {
                Workspace = workspace,
                QuickShare = quickShare,
                ExpiresAt = expiresAt
            })
        };

        public static AuditLogEntry PasswordUpdatedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            QuickShareRef quickShare,
            bool isSet) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.QuickShare,
            EventType = AuditLogEventTypes.QuickShare.PasswordUpdated,
            Severity = AuditLogSeverities.Warning,
            WorkspaceExternalId = workspace.ExternalId.Value,
            DetailsJson = Json.Serialize(new PasswordUpdated
            {
                Workspace = workspace,
                QuickShare = quickShare,
                IsSet = isSet
            })
        };

        public static AuditLogEntry MaxDownloadsUpdatedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            QuickShareRef quickShare,
            int? maxDownloads) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.QuickShare,
            EventType = AuditLogEventTypes.QuickShare.MaxDownloadsUpdated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            DetailsJson = Json.Serialize(new MaxDownloadsUpdated
            {
                Workspace = workspace,
                QuickShare = quickShare,
                MaxDownloads = maxDownloads
            })
        };

        public static AuditLogEntry ModeUpdatedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            QuickShareRef quickShare,
            QuickShareMode mode,
            bool allowIndividualFileDownload) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.QuickShare,
            EventType = AuditLogEventTypes.QuickShare.ModeUpdated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            DetailsJson = Json.Serialize(new ModeUpdated
            {
                Workspace = workspace,
                QuickShare = quickShare,
                Mode = mode,
                AllowIndividualFileDownload = allowIndividualFileDownload
            })
        };

        public static AuditLogEntry ItemsUpdatedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            QuickShareRef quickShare,
            List<FileRef> selectedFiles,
            List<FolderRef> selectedFolders,
            List<FileRef> excludedFiles,
            List<FolderRef> excludedFolders) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.QuickShare,
            EventType = AuditLogEventTypes.QuickShare.ItemsUpdated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            DetailsJson = Json.Serialize(new ItemsUpdated
            {
                Workspace = workspace,
                QuickShare = quickShare,
                SelectedFiles = selectedFiles,
                SelectedFolders = selectedFolders,
                ExcludedFiles = excludedFiles,
                ExcludedFolders = excludedFolders
            })
        };

        public static AuditLogEntry UnlockedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            QuickShareRef quickShare) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.QuickShare,
            EventType = AuditLogEventTypes.QuickShare.Unlocked,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            DetailsJson = Json.Serialize(new Unlocked
            {
                Workspace = workspace,
                QuickShare = quickShare
            })
        };

        public static AuditLogEntry UnlockFailedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            QuickShareRef quickShare) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.QuickShare,
            EventType = AuditLogEventTypes.QuickShare.UnlockFailed,
            Severity = AuditLogSeverities.Warning,
            WorkspaceExternalId = workspace.ExternalId.Value,
            DetailsJson = Json.Serialize(new UnlockFailed
            {
                Workspace = workspace,
                QuickShare = quickShare
            })
        };

        public static AuditLogEntry BulkDownloadLinkGeneratedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            QuickShareRef quickShare,
            int downloadsCountAfter) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.QuickShare,
            EventType = AuditLogEventTypes.QuickShare.BulkDownloadLinkGenerated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            DetailsJson = Json.Serialize(new BulkDownloadLinkGenerated
            {
                Workspace = workspace,
                QuickShare = quickShare,
                DownloadsCountAfter = downloadsCountAfter
            })
        };

        public static AuditLogEntry FileDownloadLinkGeneratedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            QuickShareRef quickShare,
            FileRef file,
            int downloadsCountAfter) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.QuickShare,
            EventType = AuditLogEventTypes.QuickShare.FileDownloadLinkGenerated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            DetailsJson = Json.Serialize(new FileDownloadLinkGenerated
            {
                Workspace = workspace,
                QuickShare = quickShare,
                File = file,
                DownloadsCountAfter = downloadsCountAfter
            })
        };

        public static AuditLogEntry FilePreviewLinkGeneratedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            QuickShareRef quickShare,
            FileRef file) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.QuickShare,
            EventType = AuditLogEventTypes.QuickShare.FilePreviewLinkGenerated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            DetailsJson = Json.Serialize(new FilePreviewLinkGenerated
            {
                Workspace = workspace,
                QuickShare = quickShare,
                File = file
            })
        };

        public static AuditLogEntry DownloadLimitReachedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            QuickShareRef quickShare) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.QuickShare,
            EventType = AuditLogEventTypes.QuickShare.DownloadLimitReached,
            Severity = AuditLogSeverities.Warning,
            WorkspaceExternalId = workspace.ExternalId.Value,
            DetailsJson = Json.Serialize(new DownloadLimitReached
            {
                Workspace = workspace,
                QuickShare = quickShare
            })
        };
    }
}
