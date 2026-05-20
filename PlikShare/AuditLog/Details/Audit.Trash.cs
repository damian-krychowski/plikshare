using PlikShare.Core.Utils;

namespace PlikShare.AuditLog.Details;

public static partial class Audit
{
    public static class Trash
    {
        public class ItemsRestored
        {
            public required StorageRef Storage { get; init; }
            public required WorkspaceRef Workspace { get; init; }
            public required int Count { get; init; }
        }

        public class ItemsPermanentlyDeleted
        {
            public required StorageRef Storage { get; init; }
            public required WorkspaceRef Workspace { get; init; }
            public required int Count { get; init; }
        }

        public class Emptied
        {
            public required StorageRef Storage { get; init; }
            public required WorkspaceRef Workspace { get; init; }
            public required int Count { get; init; }
        }

        public static AuditLogEntry ItemsRestoredEntry(
            AuditLogActorContext actor,
            StorageRef storage,
            WorkspaceRef workspace,
            int count) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Workspace,
            EventType = AuditLogEventTypes.Trash.ItemsRestored,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            DetailsJson = Json.Serialize(new ItemsRestored {
                Storage = storage,
                Workspace = workspace,
                Count = count })
        };

        public static AuditLogEntry ItemsPermanentlyDeletedEntry(
            AuditLogActorContext actor,
            StorageRef storage,
            WorkspaceRef workspace,
            int count) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Workspace,
            EventType = AuditLogEventTypes.Trash.ItemsPermanentlyDeleted,
            Severity = AuditLogSeverities.Critical,
            WorkspaceExternalId = workspace.ExternalId.Value,
            DetailsJson = Json.Serialize(new ItemsPermanentlyDeleted {
                Storage = storage,
                Workspace = workspace,
                Count = count })
        };

        public static AuditLogEntry EmptiedEntry(
            AuditLogActorContext actor,
            StorageRef storage,
            WorkspaceRef workspace,
            int count) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Workspace,
            EventType = AuditLogEventTypes.Trash.Emptied,
            Severity = AuditLogSeverities.Critical,
            WorkspaceExternalId = workspace.ExternalId.Value,
            DetailsJson = Json.Serialize(new Emptied {
                Storage = storage,
                Workspace = workspace,
                Count = count })
        };
    }
}
