using PlikShare.Core.Utils;
using PlikShare.Users.PermissionsAndRoles;

namespace PlikShare.AuditLog.Details;

public static partial class Audit
{
    public static class User
    {
        public class Invited
        {
            public required List<UserRef> Users { get; init; }
        }

        public class Deleted
        {
            public required UserRef Target { get; init; }
        }

        public class PermissionsAndRolesUpdated
        {
            public required UserRef Target { get; init; }
            public required bool IsAdmin { get; init; }
            public required List<string> Permissions { get; init; }
        }

        public class LimitUpdated
        {
            public required UserRef Target { get; init; }
            public required long? Value { get; init; }
        }

        public static AuditLogEntry InvitedEntry(
            AuditLogActorContext actor,
            List<UserRef> users) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.User,
            EventType = AuditLogEventTypes.User.Invited,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new Invited {
                Users = users })
        };

        public static AuditLogEntry DeletedEntry(
            AuditLogActorContext actor,
            UserRef target) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.User,
            EventType = AuditLogEventTypes.User.Deleted,
            Severity = AuditLogSeverities.Critical,
            DetailsJson = Json.Serialize(new Deleted {
                Target = target })
        };

        public static AuditLogEntry PermissionsAndRolesUpdatedEntry(
            AuditLogActorContext actor,
            UserRef target,
            UserPermissionsAndRolesDto request) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.User,
            EventType = AuditLogEventTypes.User.PermissionsAndRolesUpdated,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new PermissionsAndRolesUpdated
            {
                Target = target,
                IsAdmin = request.IsAdmin,
                Permissions = request.GetPermissionsList()
            })
        };

        public static AuditLogEntry MaxWorkspaceNumberUpdatedEntry(
            AuditLogActorContext actor,
            UserRef target,
            int? value) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.User,
            EventType = AuditLogEventTypes.User.MaxWorkspaceNumberUpdated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new LimitUpdated {
                Target = target,
                Value = value })
        };

        public static AuditLogEntry DefaultMaxWorkspaceSizeUpdatedEntry(
            AuditLogActorContext actor,
            UserRef target,
            long? value) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.User,
            EventType = AuditLogEventTypes.User.DefaultMaxWorkspaceSizeUpdated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new LimitUpdated {
                Target = target,
                Value = value })
        };

        public static AuditLogEntry DefaultMaxWorkspaceTeamMembersUpdatedEntry(
            AuditLogActorContext actor,
            UserRef target,
            int? value) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.User,
            EventType = AuditLogEventTypes.User.DefaultMaxWorkspaceTeamMembersUpdated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new LimitUpdated {
                Target = target,
                Value = value })
        };
    }
}
