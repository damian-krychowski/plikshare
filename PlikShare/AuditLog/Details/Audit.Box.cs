using PlikShare.Boxes.Permissions;
using PlikShare.BoxLinks.Id;
using PlikShare.Core.Utils;

namespace PlikShare.AuditLog.Details;

public static partial class Audit
{
    public static class Box
    {
        public class Created
        {
            public required WorkspaceRef Workspace { get; init; }
            public required BoxRef Box { get; init; }
        }

        public class Deleted
        {
            public required WorkspaceRef Workspace { get; init; }
            public required BoxRef Box { get; init; }
        }

        public class NameUpdated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required BoxRef Box { get; init; }
        }

        public class HeaderIsEnabledUpdated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required BoxRef Box { get; init; }
            public required bool IsEnabled { get; init; }
        }

        public class HeaderUpdated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required BoxRef Box { get; init; }
            public required string ContentJson { get; init; }
        }

        public class FooterIsEnabledUpdated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required BoxRef Box { get; init; }
            public required bool IsEnabled { get; init; }
        }

        public class FooterUpdated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required BoxRef Box { get; init; }
            public required string ContentJson { get; init; }
        }

        public class FolderUpdated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required BoxRef Box { get; init; }
            public required FolderRef NewFolder { get; init; }
        }

        public class IsEnabledUpdated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required BoxRef Box { get; init; }
            public required bool IsEnabled { get; init; }
        }

        public class MemberInvited
        {
            public required WorkspaceRef Workspace { get; init; }
            public required BoxRef Box { get; init; }
            public required List<UserRef> Members { get; init; }
        }

        public class MemberRevoked
        {
            public required WorkspaceRef Workspace { get; init; }
            public required BoxRef Box { get; init; }
            public required UserRef Member { get; init; }
        }

        public class MemberPermissionsUpdated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required BoxRef Box { get; init; }
            public required UserRef Member { get; init; }
            public required BoxPermissions Permissions { get; init; }
        }

        public class LinkCreated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required BoxRef Box { get; init; }
            public required BoxLinkExtId LinkExternalId { get; init; }
            public required string LinkName { get; init; }
        }

        public class InvitationAccepted
        {
            public required WorkspaceRef Workspace { get; init; }
            public required BoxRef Box { get; init; }
        }

        public class InvitationRejected
        {
            public required WorkspaceRef Workspace { get; init; }
            public required BoxRef Box { get; init; }
        }

        public class MemberLeft
        {
            public required WorkspaceRef Workspace { get; init; }
            public required BoxRef Box { get; init; }
        }

        public static AuditLogEntry CreatedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            BoxRef box) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.Created,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box.ExternalId.Value,
            DetailsJson = Json.Serialize(new Created {
                Workspace = workspace,
                Box = box })
        };

        public static AuditLogEntry DeletedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            BoxRef box) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.Deleted,
            Severity = AuditLogSeverities.Warning,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box.ExternalId.Value,
            DetailsJson = Json.Serialize(new Deleted {
                Workspace = workspace,
                Box = box })
        };

        public static AuditLogEntry NameUpdatedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            BoxRef box) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.NameUpdated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box.ExternalId.Value,
            DetailsJson = Json.Serialize(new NameUpdated {
                Workspace = workspace,
                Box = box })
        };

        public static AuditLogEntry HeaderIsEnabledUpdatedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            BoxRef box,
            bool isEnabled) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.HeaderIsEnabledUpdated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box.ExternalId.Value,
            DetailsJson = Json.Serialize(new HeaderIsEnabledUpdated {
                Workspace = workspace,
                Box = box,
                IsEnabled = isEnabled })
        };

        public static AuditLogEntry HeaderUpdatedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            BoxRef box,
            string contentJson) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.HeaderUpdated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box.ExternalId.Value,
            DetailsJson = Json.Serialize(new HeaderUpdated {
                Workspace = workspace,
                Box = box,
                ContentJson = contentJson })
        };

        public static AuditLogEntry FooterIsEnabledUpdatedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            BoxRef box,
            bool isEnabled) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.FooterIsEnabledUpdated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box.ExternalId.Value,
            DetailsJson = Json.Serialize(new FooterIsEnabledUpdated {
                Workspace = workspace,
                Box = box,
                IsEnabled = isEnabled })
        };

        public static AuditLogEntry FooterUpdatedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            BoxRef box,
            string contentJson) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.FooterUpdated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box.ExternalId.Value,
            DetailsJson = Json.Serialize(new FooterUpdated {
                Workspace = workspace,
                Box = box,
                ContentJson = contentJson })
        };

        public static AuditLogEntry FolderUpdatedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            BoxRef box,
            FolderRef newFolder) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.FolderUpdated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box.ExternalId.Value,
            DetailsJson = Json.Serialize(new FolderUpdated {
                Workspace = workspace,
                Box = box,
                NewFolder = newFolder })
        };

        public static AuditLogEntry IsEnabledUpdatedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            BoxRef box,
            bool isEnabled) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.IsEnabledUpdated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box.ExternalId.Value,
            DetailsJson = Json.Serialize(new IsEnabledUpdated {
                Workspace = workspace,
                Box = box,
                IsEnabled = isEnabled })
        };

        public static AuditLogEntry MemberInvitedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            BoxRef box,
            List<UserRef> members) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.MemberInvited,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box.ExternalId.Value,
            DetailsJson = Json.Serialize(new MemberInvited {
                Workspace = workspace,
                Box = box,
                Members = members })
        };

        public static AuditLogEntry MemberRevokedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            BoxRef box,
            UserRef member) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.MemberRevoked,
            Severity = AuditLogSeverities.Warning,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box.ExternalId.Value,
            DetailsJson = Json.Serialize(new MemberRevoked {
                Workspace = workspace,
                Box = box,
                Member = member })
        };

        public static AuditLogEntry MemberPermissionsUpdatedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            BoxRef box,
            UserRef member,
            BoxPermissions permissions) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.MemberPermissionsUpdated,
            Severity = AuditLogSeverities.Warning,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box.ExternalId.Value,
            DetailsJson = Json.Serialize(new MemberPermissionsUpdated {
                Workspace = workspace,
                Box = box,
                Member = member,
                Permissions = permissions })
        };

        public static AuditLogEntry LinkCreatedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            BoxRef box,
            BoxLinkExtId linkExternalId,
            string linkName) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.LinkCreated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box.ExternalId.Value,
            DetailsJson = Json.Serialize(new LinkCreated {
                Workspace = workspace,
                Box = box,
                LinkExternalId = linkExternalId,
                LinkName = linkName })
        };

        public static AuditLogEntry InvitationAcceptedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            BoxRef box) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.InvitationAccepted,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box.ExternalId.Value,
            DetailsJson = Json.Serialize(new InvitationAccepted {
                Workspace = workspace,
                Box = box })
        };

        public static AuditLogEntry InvitationRejectedEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            BoxRef box) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.InvitationRejected,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box.ExternalId.Value,
            DetailsJson = Json.Serialize(new InvitationRejected {
                Workspace = workspace,
                Box = box })
        };

        public static AuditLogEntry MemberLeftEntry(
            AuditLogActorContext actor,
            WorkspaceRef workspace,
            BoxRef box) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.MemberLeft,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box.ExternalId.Value,
            DetailsJson = Json.Serialize(new MemberLeft {
                Workspace = workspace,
                Box = box })
        };
    }
}
