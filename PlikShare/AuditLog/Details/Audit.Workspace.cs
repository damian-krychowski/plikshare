using PlikShare.Core.Utils;

namespace PlikShare.AuditLog.Details;

public static partial class Audit
{
    public static class Workspace
    {
        public class Created
        {
            public required StorageRef Storage { get; init; }
            public required WorkspaceRef Workspace { get; init; }
            public required long? MaxSizeInBytes { get; init; }
            public required string BucketName { get; init; }
        }

        public class Deleted
        {
            public required StorageRef Storage { get; init; }
            public required WorkspaceRef Workspace { get; init; }
        }

        public class NameUpdated
        {
            public required StorageRef Storage { get; init; }
            public required WorkspaceRef Workspace { get; init; }
        }

        public class OwnerChanged
        {
            public required StorageRef Storage { get; init; }
            public required WorkspaceRef Workspace { get; init; }
            public required UserRef NewOwner { get; init; }
        }

        public class MaxSizeUpdated
        {
            public required StorageRef Storage { get; init; }
            public required WorkspaceRef Workspace { get; init; }
            public required long? Value { get; init; }
        }

        public class MaxTeamMembersUpdated
        {
            public required StorageRef Storage { get; init; }
            public required WorkspaceRef Workspace { get; init; }
            public required int? Value { get; init; }
        }

        public class MemberInvited
        {
            public required StorageRef Storage { get; init; }
            public required WorkspaceRef Workspace { get; init; }
            public required List<UserRef> Members { get; init; }
        }

        public class MemberRevoked
        {
            public required StorageRef Storage { get; init; }
            public required WorkspaceRef Workspace { get; init; }
            public required UserRef Member { get; init; }
        }

        public class MemberPermissionsUpdated
        {
            public required StorageRef Storage { get; init; }
            public required WorkspaceRef Workspace { get; init; }
            public required UserRef Member { get; init; }
            public required bool AllowShare { get; init; }
        }

        public class InvitationResponse
        {
            public required StorageRef Storage { get; init; }
            public required WorkspaceRef Workspace { get; init; }
        }

        public class MemberLeft
        {
            public required StorageRef Storage { get; init; }
            public required WorkspaceRef Workspace { get; init; }
        }

        public class BulkDeleteRequested
        {
            public required StorageRef Storage { get; init; }
            public required WorkspaceRef Workspace { get; init; }
            public required List<FileRef> Files { get; init; }
            public required List<FolderRef> Folders { get; init; }
            public required List<FileUploadRef> FileUploads { get; init; }
        }

        public static AuditLogEntry CreatedEntry(
            AuditLogActorContext actor,
            StorageRef storage,
            WorkspaceRef workspace,
            long? maxSizeInBytes,
            string bucketName) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Workspace,
            EventType = AuditLogEventTypes.Workspace.Created,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            DetailsJson = Json.Serialize(new Created {
                Storage = storage,
                Workspace = workspace,
                MaxSizeInBytes = maxSizeInBytes,
                BucketName = bucketName })
        };

        public static AuditLogEntry DeletedEntry(
            AuditLogActorContext actor,
            StorageRef storage,
            WorkspaceRef workspace) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Workspace,
            EventType = AuditLogEventTypes.Workspace.Deleted,
            Severity = AuditLogSeverities.Critical,
            WorkspaceExternalId = workspace.ExternalId.Value,
            DetailsJson = Json.Serialize(new Deleted {
                Storage = storage,
                Workspace = workspace })
        };

        public static AuditLogEntry NameUpdatedEntry(
            AuditLogActorContext actor,
            StorageRef storage,
            WorkspaceRef workspace) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Workspace,
            EventType = AuditLogEventTypes.Workspace.NameUpdated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            DetailsJson = Json.Serialize(new NameUpdated {
                Storage = storage,
                Workspace = workspace })
        };

        public static AuditLogEntry OwnerChangedEntry(
            AuditLogActorContext actor,
            StorageRef storage,
            WorkspaceRef workspace,
            UserRef newOwner) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Workspace,
            EventType = AuditLogEventTypes.Workspace.OwnerChanged,
            Severity = AuditLogSeverities.Warning,
            WorkspaceExternalId = workspace.ExternalId.Value,
            DetailsJson = Json.Serialize(new OwnerChanged {
                Storage = storage,
                Workspace = workspace,
                NewOwner = newOwner })
        };

        public static AuditLogEntry MaxSizeUpdatedEntry(
            AuditLogActorContext actor,
            StorageRef storage,
            WorkspaceRef workspace,
            long? value) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Workspace,
            EventType = AuditLogEventTypes.Workspace.MaxSizeUpdated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            DetailsJson = Json.Serialize(new MaxSizeUpdated {
                Storage = storage,
                Workspace = workspace,
                Value = value })
        };

        public static AuditLogEntry MaxTeamMembersUpdatedEntry(
            AuditLogActorContext actor,
            StorageRef storage,
            WorkspaceRef workspace,
            int? value) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Workspace,
            EventType = AuditLogEventTypes.Workspace.MaxTeamMembersUpdated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            DetailsJson = Json.Serialize(new MaxTeamMembersUpdated {
                Storage = storage,
                Workspace = workspace,
                Value = value })
        };

        public static AuditLogEntry MemberInvitedEntry(
            AuditLogActorContext actor,
            StorageRef storage,
            WorkspaceRef workspace,
            List<UserRef> members) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Workspace,
            EventType = AuditLogEventTypes.Workspace.MemberInvited,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            DetailsJson = Json.Serialize(new MemberInvited {
                Storage = storage,
                Workspace = workspace,
                Members = members })
        };

        public static AuditLogEntry MemberRevokedEntry(
            AuditLogActorContext actor,
            StorageRef storage,
            WorkspaceRef workspace,
            UserRef member) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Workspace,
            EventType = AuditLogEventTypes.Workspace.MemberRevoked,
            Severity = AuditLogSeverities.Warning,
            WorkspaceExternalId = workspace.ExternalId.Value,
            DetailsJson = Json.Serialize(new MemberRevoked {
                Storage = storage,
                Workspace = workspace,
                Member = member })
        };

        public static AuditLogEntry MemberPermissionsUpdatedEntry(
            AuditLogActorContext actor,
            StorageRef storage,
            WorkspaceRef workspace,
            UserRef member,
            bool allowShare) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Workspace,
            EventType = AuditLogEventTypes.Workspace.MemberPermissionsUpdated,
            Severity = AuditLogSeverities.Warning,
            WorkspaceExternalId = workspace.ExternalId.Value,
            DetailsJson = Json.Serialize(new MemberPermissionsUpdated {
                Storage = storage,
                Workspace = workspace,
                Member = member,
                AllowShare = allowShare })
        };

        public static AuditLogEntry InvitationAcceptedEntry(
            AuditLogActorContext actor,
            StorageRef storage,
            WorkspaceRef workspace) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Workspace,
            EventType = AuditLogEventTypes.Workspace.InvitationAccepted,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            DetailsJson = Json.Serialize(new InvitationResponse {
                Storage = storage,
                Workspace = workspace })
        };

        public static AuditLogEntry InvitationRejectedEntry(
            AuditLogActorContext actor,
            StorageRef storage,
            WorkspaceRef workspace) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Workspace,
            EventType = AuditLogEventTypes.Workspace.InvitationRejected,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            DetailsJson = Json.Serialize(new InvitationResponse {
                Storage = storage,
                Workspace = workspace })
        };

        public static AuditLogEntry MemberLeftEntry(
            AuditLogActorContext actor,
            StorageRef storage,
            WorkspaceRef workspace) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Workspace,
            EventType = AuditLogEventTypes.Workspace.MemberLeft,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            DetailsJson = Json.Serialize(new MemberLeft {
                Storage = storage,
                Workspace = workspace })
        };

        public static AuditLogEntry BulkDeleteRequestedEntry(
            AuditLogActorContext actor,
            StorageRef storage,
            WorkspaceRef workspace,
            List<FileRef> files,
            List<FolderRef> folders,
            List<FileUploadRef> fileUploads) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Workspace,
            EventType = AuditLogEventTypes.Workspace.BulkDeleteRequested,
            Severity = AuditLogSeverities.Critical,
            WorkspaceExternalId = workspace.ExternalId.Value,
            DetailsJson = Json.Serialize(new BulkDeleteRequested {
                Storage = storage,
                Workspace = workspace,
                Files = files,
                Folders = folders,
                FileUploads = fileUploads })
        };
    }
}
