using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;
using PlikShare.Folders.Id;

namespace PlikShare.AuditLog.Details;

public static partial class Audit
{
    public static class File
    {
        public class Renamed
        {
            public required WorkspaceRef Workspace { get; init; }
            public required FileRef File { get; init; }
            public BoxAccessRef? Box { get; init; }
        }

        public class NoteSaved
        {
            public required WorkspaceRef Workspace { get; init; }
            public required FileRef File { get; init; }
            public BoxAccessRef? Box { get; init; }
        }

        public class CommentCreated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required FileRef File { get; init; }
            public required FileArtifactExtId CommentExternalId { get; init; }
            public required EncodedMetadataValue ContentJson { get; init; }
            public BoxAccessRef? Box { get; init; }
        }

        public class CommentDeleted
        {
            public required WorkspaceRef Workspace { get; init; }
            public required FileRef File { get; init; }
            public required FileArtifactExtId CommentExternalId { get; init; }
            public BoxAccessRef? Box { get; init; }
        }

        public class CommentEdited
        {
            public required WorkspaceRef Workspace { get; init; }
            public required FileRef File { get; init; }
            public required FileArtifactExtId CommentExternalId { get; init; }
            public required EncodedMetadataValue ContentJson { get; init; }
            public BoxAccessRef? Box { get; init; }
        }

        public class ContentUpdated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required FileRef File { get; init; }
            public BoxAccessRef? Box { get; init; }
        }

        public class AttachmentUploaded
        {
            public required WorkspaceRef Workspace { get; init; }
            public required FileRef ParentFile { get; init; }
            public required FileRef Attachment { get; init; }
            public BoxAccessRef? Box { get; init; }
        }

        public class DownloadLinkGenerated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required FileRef File { get; init; }
            public BoxAccessRef? Box { get; init; }
        }

        public class BulkDownloadLinkGenerated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required List<FileExtId> SelectedFileExternalIds { get; init; }
            public required List<FolderExtId> SelectedFolderExternalIds { get; init; }
            public BoxAccessRef? Box { get; init; }
        }

        public class Downloaded
        {
            public required WorkspaceRef Workspace { get; init; }
            public required FileRef File { get; init; }
            public BoxAccessRef? Box { get; init; }
        }

        public class BulkDownloaded
        {
            public required WorkspaceRef Workspace { get; init; }
            public required List<FileRef> Files { get; init; }
            public BoxAccessRef? Box { get; init; }
        }

        public class UploadInitiated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required List<FileUploadRef> FileUploads { get; init; }
            public BoxAccessRef? Box { get; init; }
        }

        public class MultiUploadCompleted
        {
            public required WorkspaceRef Workspace { get; init; }
            public required List<FileUploadRef> FileUploads { get; init; }
            public BoxAccessRef? Box { get; init; }
        }

        public class UploadCompleted
        {
            public required WorkspaceRef Workspace { get; init; }
            public required FileUploadRef FileUpload { get; init; }
            public BoxAccessRef? Box { get; init; }
        }

        public static AuditLogEntry RenamedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            FileRef file,
            BoxAccessRef? box = null) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.File,
            EventType = AuditLogEventTypes.File.Renamed,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box?.ExternalId.Value,
            BoxLinkExternalId = box?.BoxLink?.ExternalId.Value,
            DetailsJson = Json.Serialize(new Renamed {
                Workspace = workspace,
                File = file,
                Box = box })
        };

        public static AuditLogEntry NoteSavedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            FileRef file,
            BoxAccessRef? box = null) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.File,
            EventType = AuditLogEventTypes.File.NoteSaved,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box?.ExternalId.Value,
            BoxLinkExternalId = box?.BoxLink?.ExternalId.Value,
            DetailsJson = Json.Serialize(new NoteSaved {
                Workspace = workspace,
                File = file,
                Box = box })
        };

        public static AuditLogEntry CommentCreatedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            FileRef file,
            FileArtifactExtId commentExternalId,
            EncodedMetadataValue contentJson,
            BoxAccessRef? box = null) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.File,
            EventType = AuditLogEventTypes.File.CommentCreated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box?.ExternalId.Value,
            BoxLinkExternalId = box?.BoxLink?.ExternalId.Value,
            DetailsJson = Json.Serialize(new CommentCreated {
                Workspace = workspace,
                File = file,
                CommentExternalId = commentExternalId,
                ContentJson = contentJson,
                Box = box })
        };

        public static AuditLogEntry CommentDeletedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            FileRef file,
            FileArtifactExtId commentExternalId,
            BoxAccessRef? box = null) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.File,
            EventType = AuditLogEventTypes.File.CommentDeleted,
            Severity = AuditLogSeverities.Warning,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box?.ExternalId.Value,
            BoxLinkExternalId = box?.BoxLink?.ExternalId.Value,
            DetailsJson = Json.Serialize(new CommentDeleted {
                Workspace = workspace,
                File = file,
                CommentExternalId = commentExternalId,
                Box = box })
        };

        public static AuditLogEntry CommentEditedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            FileRef file,
            FileArtifactExtId commentExternalId,
            EncodedMetadataValue contentJson,
            BoxAccessRef? box = null) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.File,
            EventType = AuditLogEventTypes.File.CommentEdited,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box?.ExternalId.Value,
            BoxLinkExternalId = box?.BoxLink?.ExternalId.Value,
            DetailsJson = Json.Serialize(new CommentEdited {
                Workspace = workspace,
                File = file,
                CommentExternalId = commentExternalId,
                ContentJson = contentJson,
                Box = box })
        };

        public static AuditLogEntry ContentUpdatedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            FileRef file,
            BoxAccessRef? box = null) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.File,
            EventType = AuditLogEventTypes.File.ContentUpdated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box?.ExternalId.Value,
            BoxLinkExternalId = box?.BoxLink?.ExternalId.Value,
            DetailsJson = Json.Serialize(new ContentUpdated {
                Workspace = workspace,
                File = file,
                Box = box })
        };

        public static AuditLogEntry AttachmentUploadedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            FileRef parentFile,
            FileRef attachment,
            BoxAccessRef? box = null) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.File,
            EventType = AuditLogEventTypes.File.AttachmentUploaded,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box?.ExternalId.Value,
            BoxLinkExternalId = box?.BoxLink?.ExternalId.Value,
            DetailsJson = Json.Serialize(new AttachmentUploaded {
                Workspace = workspace,
                ParentFile = parentFile,
                Attachment = attachment,
                Box = box })
        };

        public static AuditLogEntry DownloadLinkGeneratedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            FileRef file,
            BoxAccessRef? box = null) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.File,
            EventType = AuditLogEventTypes.File.DownloadLinkGenerated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box?.ExternalId.Value,
            BoxLinkExternalId = box?.BoxLink?.ExternalId.Value,
            DetailsJson = Json.Serialize(new DownloadLinkGenerated {
                Workspace = workspace,
                File = file,
                Box = box })
        };

        public static AuditLogEntry BulkDownloadLinkGeneratedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            List<FileExtId> selectedFileExternalIds,
            List<FolderExtId> selectedFolderExternalIds,
            BoxAccessRef? box = null) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.File,
            EventType = AuditLogEventTypes.File.BulkDownloadLinkGenerated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box?.ExternalId.Value,
            BoxLinkExternalId = box?.BoxLink?.ExternalId.Value,
            DetailsJson = Json.Serialize(new BulkDownloadLinkGenerated {
                Workspace = workspace,
                SelectedFileExternalIds = selectedFileExternalIds,
                SelectedFolderExternalIds = selectedFolderExternalIds,
                Box = box })
        };

        public static AuditLogEntry DownloadedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            FileRef file,
            BoxAccessRef? box = null) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.File,
            EventType = AuditLogEventTypes.File.Downloaded,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box?.ExternalId.Value,
            BoxLinkExternalId = box?.BoxLink?.ExternalId.Value,
            DetailsJson = Json.Serialize(new Downloaded {
                Workspace = workspace,
                File = file,
                Box = box })
        };

        public static AuditLogEntry BulkDownloadedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            List<FileRef> files,
            BoxAccessRef? box = null) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.File,
            EventType = AuditLogEventTypes.File.BulkDownloaded,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box?.ExternalId.Value,
            BoxLinkExternalId = box?.BoxLink?.ExternalId.Value,
            DetailsJson = Json.Serialize(new BulkDownloaded {
                Workspace = workspace,
                Files = files,
                Box = box })
        };

        public static AuditLogEntry UploadInitiatedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            List<FileUploadRef> fileUploads,
            BoxAccessRef? box = null) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.File,
            EventType = AuditLogEventTypes.File.UploadInitiated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box?.ExternalId.Value,
            BoxLinkExternalId = box?.BoxLink?.ExternalId.Value,
            DetailsJson = Json.Serialize(new UploadInitiated {
                Workspace = workspace,
                FileUploads = fileUploads,
                Box = box })
        };

        public static AuditLogEntry UploadCompletedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            FileUploadRef fileUpload,
            BoxAccessRef? box = null) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.File,
            EventType = AuditLogEventTypes.File.UploadCompleted,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box?.ExternalId.Value,
            BoxLinkExternalId = box?.BoxLink?.ExternalId.Value,
            DetailsJson = Json.Serialize(new UploadCompleted {
                Workspace = workspace,
                FileUpload = fileUpload,
                Box = box })
        };

        public static AuditLogEntry MultiUploadCompletedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            List<FileUploadRef> fileUploads,
            BoxAccessRef? box = null) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.File,
            EventType = AuditLogEventTypes.File.MultiUploadCompleted,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box?.ExternalId.Value,
            BoxLinkExternalId = box?.BoxLink?.ExternalId.Value,
            DetailsJson = Json.Serialize(new MultiUploadCompleted {
                Workspace = workspace,
                FileUploads = fileUploads,
                Box = box })
        };
    }
}
