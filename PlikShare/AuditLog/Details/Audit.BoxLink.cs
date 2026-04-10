using PlikShare.Boxes.Permissions;
using PlikShare.Core.Utils;

namespace PlikShare.AuditLog.Details;

public static partial class Audit
{
    public static class BoxLink
    {
        public class Deleted
        {
            public required WorkspaceRef Workspace { get; init; }
            public required BoxRef Box { get; init; }
            public required BoxLinkRef BoxLink { get; init; }
        }

        public class NameUpdated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required BoxRef Box { get; init; }
            public required BoxLinkRef BoxLink { get; init; }
        }

        public class WidgetOriginsUpdated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required BoxRef Box { get; init; }
            public required BoxLinkRef BoxLink { get; init; }
            public required List<string> WidgetOrigins { get; init; }
        }

        public class IsEnabledUpdated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required BoxRef Box { get; init; }
            public required BoxLinkRef BoxLink { get; init; }
            public required bool IsEnabled { get; init; }
        }

        public class PermissionsUpdated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required BoxRef Box { get; init; }
            public required BoxLinkRef BoxLink { get; init; }
            public required BoxPermissions Permissions { get; init; }
        }

        public class AccessCodeRegenerated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required BoxRef Box { get; init; }
            public required BoxLinkRef BoxLink { get; init; }
        }

        public static AuditLogEntry DeletedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            BoxRef box,
            BoxLinkRef boxLink) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.BoxLink,
            EventType = AuditLogEventTypes.BoxLink.Deleted,
            Severity = AuditLogSeverities.Warning,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box.ExternalId.Value,
            BoxLinkExternalId = boxLink.ExternalId.Value,
            DetailsJson = Json.Serialize(new Deleted {
                Workspace = workspace,
                Box = box,
                BoxLink = boxLink })
        };

        public static AuditLogEntry NameUpdatedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            BoxRef box,
            BoxLinkRef boxLink) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.BoxLink,
            EventType = AuditLogEventTypes.BoxLink.NameUpdated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box.ExternalId.Value,
            BoxLinkExternalId = boxLink.ExternalId.Value,
            DetailsJson = Json.Serialize(new NameUpdated {
                Workspace = workspace,
                Box = box,
                BoxLink = boxLink })
        };

        public static AuditLogEntry WidgetOriginsUpdatedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            BoxRef box,
            BoxLinkRef boxLink,
            List<string> widgetOrigins) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.BoxLink,
            EventType = AuditLogEventTypes.BoxLink.WidgetOriginsUpdated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box.ExternalId.Value,
            BoxLinkExternalId = boxLink.ExternalId.Value,
            DetailsJson = Json.Serialize(new WidgetOriginsUpdated {
                Workspace = workspace,
                Box = box,
                BoxLink = boxLink,
                WidgetOrigins = widgetOrigins })
        };

        public static AuditLogEntry IsEnabledUpdatedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            BoxRef box,
            BoxLinkRef boxLink,
            bool isEnabled) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.BoxLink,
            EventType = AuditLogEventTypes.BoxLink.IsEnabledUpdated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box.ExternalId.Value,
            BoxLinkExternalId = boxLink.ExternalId.Value,
            DetailsJson = Json.Serialize(new IsEnabledUpdated {
                Workspace = workspace,
                Box = box,
                BoxLink = boxLink,
                IsEnabled = isEnabled })
        };

        public static AuditLogEntry PermissionsUpdatedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            BoxRef box,
            BoxLinkRef boxLink,
            BoxPermissions permissions) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.BoxLink,
            EventType = AuditLogEventTypes.BoxLink.PermissionsUpdated,
            Severity = AuditLogSeverities.Warning,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box.ExternalId.Value,
            BoxLinkExternalId = boxLink.ExternalId.Value,
            DetailsJson = Json.Serialize(new PermissionsUpdated {
                Workspace = workspace,
                Box = box,
                BoxLink = boxLink,
                Permissions = permissions })
        };

        public static AuditLogEntry AccessCodeRegeneratedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            BoxRef box,
            BoxLinkRef boxLink) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.BoxLink,
            EventType = AuditLogEventTypes.BoxLink.AccessCodeRegenerated,
            Severity = AuditLogSeverities.Warning,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box.ExternalId.Value,
            BoxLinkExternalId = boxLink.ExternalId.Value,
            DetailsJson = Json.Serialize(new AccessCodeRegenerated {
                Workspace = workspace,
                Box = box,
                BoxLink = boxLink })
        };
    }
}
