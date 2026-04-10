using PlikShare.Core.Utils;

namespace PlikShare.AuditLog.Details;

public static partial class Audit
{
    public static class Folder
    {
        public class Created
        {
            public required WorkspaceRef Workspace { get; init; }
            public required FolderRef Folder { get; init; }
            public BoxAccessRef? Box { get; init; }
        }

        public class BulkCreated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required List<FolderRef> Folders { get; init; }
            public BoxAccessRef? Box { get; init; }
        }

        public class NameUpdated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required FolderRef Folder { get; init; }
            public BoxAccessRef? Box { get; init; }
        }

        public class ItemsMoved
        {
            public required WorkspaceRef Workspace { get; init; }
            public required FolderRef? DestinationFolder { get; init; }
            public required List<FolderRef> Folders { get; init; }
            public required List<FileRef> Files { get; init; }
            public required List<FileUploadRef> FileUploads { get; init; }
            public BoxAccessRef? Box { get; init; }
        }

        public static AuditLogEntry CreatedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            FolderRef folder,
            BoxAccessRef? box = null) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Folder,
            EventType = AuditLogEventTypes.Folder.Created,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box?.ExternalId.Value,
            BoxLinkExternalId = box?.BoxLink?.ExternalId.Value,
            DetailsJson = Json.Serialize(new Created {
                Workspace = workspace,
                Folder = folder,
                Box = box })
        };

        public static AuditLogEntry BulkCreatedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            List<FolderRef> folders,
            BoxAccessRef? box = null) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Folder,
            EventType = AuditLogEventTypes.Folder.BulkCreated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box?.ExternalId.Value,
            BoxLinkExternalId = box?.BoxLink?.ExternalId.Value,
            DetailsJson = Json.Serialize(new BulkCreated {
                Workspace = workspace,
                Folders = folders,
                Box = box })
        };

        public static AuditLogEntry NameUpdatedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            FolderRef folder,
            BoxAccessRef? box = null) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Folder,
            EventType = AuditLogEventTypes.Folder.NameUpdated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box?.ExternalId.Value,
            BoxLinkExternalId = box?.BoxLink?.ExternalId.Value,
            DetailsJson = Json.Serialize(new NameUpdated {
                Workspace = workspace,
                Folder = folder,
                Box = box })
        };

        public static AuditLogEntry ItemsMovedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            FolderRef? destinationFolder,
            List<FolderRef> folders,
            List<FileRef> files,
            List<FileUploadRef> fileUploads,
            BoxAccessRef? box = null) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Folder,
            EventType = AuditLogEventTypes.Folder.ItemsMoved,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box?.ExternalId.Value,
            BoxLinkExternalId = box?.BoxLink?.ExternalId.Value,
            DetailsJson = Json.Serialize(new ItemsMoved {
                Workspace = workspace,
                DestinationFolder = destinationFolder,
                Folders = folders,
                Files = files,
                FileUploads = fileUploads,
                Box = box })
        };
    }
}
