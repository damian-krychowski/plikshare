using PlikShare.Core.Utils;

namespace PlikShare.AuditLog.Details;

public static partial class Audit
{
    public static class Agent
    {
        public class Created
        {
            public required AgentRef Agent { get; init; }
        }

        public class Deleted
        {
            public required AgentRef Agent { get; init; }
        }

        public static AuditLogEntry CreatedEntry(
            AuditLogActorContext actor,
            AgentRef agent) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Agent,
            EventType = AuditLogEventTypes.Agent.Created,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new Created {
                Agent = agent })
        };

        public static AuditLogEntry DeletedEntry(
            AuditLogActorContext actor,
            AgentRef agent) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Agent,
            EventType = AuditLogEventTypes.Agent.Deleted,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new Deleted {
                Agent = agent })
        };

        public class TokenRotated
        {
            public required AgentRef Agent { get; init; }
        }

        public class WorkspaceAccessGranted
        {
            public required AgentRef Agent { get; init; }
            public required WorkspaceRef Workspace { get; init; }
        }

        public class WorkspaceAccessRevoked
        {
            public required AgentRef Agent { get; init; }
            public required WorkspaceRef Workspace { get; init; }
        }

        public static AuditLogEntry TokenRotatedEntry(
            AuditLogActorContext actor,
            AgentRef agent) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Agent,
            EventType = AuditLogEventTypes.Agent.TokenRotated,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new TokenRotated {
                Agent = agent })
        };

        public static AuditLogEntry WorkspaceAccessGrantedEntry(
            AuditLogActorContext actor,
            AgentRef agent,
            WorkspaceRef workspace) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Agent,
            EventType = AuditLogEventTypes.Agent.WorkspaceAccessGranted,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new WorkspaceAccessGranted {
                Agent = agent,
                Workspace = workspace })
        };

        public static AuditLogEntry WorkspaceAccessRevokedEntry(
            AuditLogActorContext actor,
            AgentRef agent,
            WorkspaceRef workspace) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Agent,
            EventType = AuditLogEventTypes.Agent.WorkspaceAccessRevoked,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new WorkspaceAccessRevoked {
                Agent = agent,
                Workspace = workspace })
        };

        public class BoxAccessGranted
        {
            public required AgentRef Agent { get; init; }
            public required BoxRef Box { get; init; }
        }

        public class BoxAccessRevoked
        {
            public required AgentRef Agent { get; init; }
            public required BoxRef Box { get; init; }
        }

        public static AuditLogEntry BoxAccessGrantedEntry(
            AuditLogActorContext actor,
            AgentRef agent,
            BoxRef box) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Agent,
            EventType = AuditLogEventTypes.Agent.BoxAccessGranted,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new BoxAccessGranted {
                Agent = agent,
                Box = box })
        };

        public static AuditLogEntry BoxAccessRevokedEntry(
            AuditLogActorContext actor,
            AgentRef agent,
            BoxRef box) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Agent,
            EventType = AuditLogEventTypes.Agent.BoxAccessRevoked,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new BoxAccessRevoked {
                Agent = agent,
                Box = box })
        };

        public class SettingsUpdated
        {
            public required AgentRef Agent { get; init; }
        }

        public static AuditLogEntry PermissionsAndRolesUpdatedEntry(
            AuditLogActorContext actor,
            AgentRef agent) => SettingsUpdatedEntry(
                actor, agent, AuditLogEventTypes.Agent.PermissionsAndRolesUpdated, AuditLogSeverities.Warning);

        public static AuditLogEntry MaxWorkspaceNumberUpdatedEntry(
            AuditLogActorContext actor,
            AgentRef agent) => SettingsUpdatedEntry(
                actor, agent, AuditLogEventTypes.Agent.MaxWorkspaceNumberUpdated, AuditLogSeverities.Info);

        public static AuditLogEntry DefaultMaxWorkspaceSizeUpdatedEntry(
            AuditLogActorContext actor,
            AgentRef agent) => SettingsUpdatedEntry(
                actor, agent, AuditLogEventTypes.Agent.DefaultMaxWorkspaceSizeUpdated, AuditLogSeverities.Info);

        public static AuditLogEntry DefaultMaxWorkspaceTeamMembersUpdatedEntry(
            AuditLogActorContext actor,
            AgentRef agent) => SettingsUpdatedEntry(
                actor, agent, AuditLogEventTypes.Agent.DefaultMaxWorkspaceTeamMembersUpdated, AuditLogSeverities.Info);

        public static AuditLogEntry StorageAccessUpdatedEntry(
            AuditLogActorContext actor,
            AgentRef agent) => SettingsUpdatedEntry(
                actor, agent, AuditLogEventTypes.Agent.StorageAccessUpdated, AuditLogSeverities.Warning);

        private static AuditLogEntry SettingsUpdatedEntry(
            AuditLogActorContext actor,
            AgentRef agent,
            string eventType,
            string severity) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Agent,
            EventType = eventType,
            Severity = severity,
            DetailsJson = Json.Serialize(new SettingsUpdated {
                Agent = agent })
        };
    }
}
