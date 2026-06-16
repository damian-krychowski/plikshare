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

        public class WorkspacesListed
        {
            public required int Count { get; init; }
        }

        public static AuditLogEntry WorkspacesListedEntry(
            AuditLogActorContext actor,
            int count) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Agent,
            EventType = AuditLogEventTypes.Agent.WorkspacesListed,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new WorkspacesListed {
                Count = count })
        };

        public class WorkspaceContentListed
        {
            public required string WorkspaceExternalId { get; init; }
            public required string? FolderExternalId { get; init; }
            public required int Count { get; init; }
        }

        public static AuditLogEntry WorkspaceContentListedEntry(
            AuditLogActorContext actor,
            string workspaceExternalId,
            string? folderExternalId,
            int count) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Agent,
            EventType = AuditLogEventTypes.Agent.WorkspaceContentListed,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new WorkspaceContentListed {
                WorkspaceExternalId = workspaceExternalId,
                FolderExternalId = folderExternalId,
                Count = count })
        };

        public class ShareLinksListed
        {
            public required string WorkspaceExternalId { get; init; }
            public required int Count { get; init; }
        }

        public static AuditLogEntry ShareLinksListedEntry(
            AuditLogActorContext actor,
            string workspaceExternalId,
            int count) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Agent,
            EventType = AuditLogEventTypes.Agent.ShareLinksListed,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new ShareLinksListed {
                WorkspaceExternalId = workspaceExternalId,
                Count = count })
        };

        public class ShareLinkViewed
        {
            public required string WorkspaceExternalId { get; init; }
            public required string ShareLinkExternalId { get; init; }
        }

        public static AuditLogEntry ShareLinkViewedEntry(
            AuditLogActorContext actor,
            string workspaceExternalId,
            string shareLinkExternalId) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Agent,
            EventType = AuditLogEventTypes.Agent.ShareLinkViewed,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new ShareLinkViewed {
                WorkspaceExternalId = workspaceExternalId,
                ShareLinkExternalId = shareLinkExternalId })
        };

        public class FileViewed
        {
            public required string WorkspaceExternalId { get; init; }
            public required string FileExternalId { get; init; }
        }

        public class FileContentRead
        {
            public required string WorkspaceExternalId { get; init; }
            public required string FileExternalId { get; init; }
            public required long Offset { get; init; }
            public required long BytesReturned { get; init; }
        }

        public class FileCreated
        {
            public required string WorkspaceExternalId { get; init; }
            public required string FileExternalId { get; init; }
            public required string? FolderExternalId { get; init; }
            public required long SizeInBytes { get; init; }
        }

        public static AuditLogEntry FileCreatedEntry(
            AuditLogActorContext actor,
            string workspaceExternalId,
            string fileExternalId,
            string? folderExternalId,
            long sizeInBytes) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Agent,
            EventType = AuditLogEventTypes.Agent.FileCreated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new FileCreated {
                WorkspaceExternalId = workspaceExternalId,
                FileExternalId = fileExternalId,
                FolderExternalId = folderExternalId,
                SizeInBytes = sizeInBytes })
        };

        public static AuditLogEntry FileContentReadEntry(
            AuditLogActorContext actor,
            string workspaceExternalId,
            string fileExternalId,
            long offset,
            long bytesReturned) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Agent,
            EventType = AuditLogEventTypes.Agent.FileContentRead,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new FileContentRead {
                WorkspaceExternalId = workspaceExternalId,
                FileExternalId = fileExternalId,
                Offset = offset,
                BytesReturned = bytesReturned })
        };

        public class FileDownloadLinkGenerated
        {
            public required string WorkspaceExternalId { get; init; }
            public required string FileExternalId { get; init; }
            public required DateTimeOffset ExpiresAt { get; init; }
        }

        public static AuditLogEntry FileDownloadLinkGeneratedEntry(
            AuditLogActorContext actor,
            string workspaceExternalId,
            string fileExternalId,
            DateTimeOffset expiresAt) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Agent,
            EventType = AuditLogEventTypes.Agent.FileDownloadLinkGenerated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new FileDownloadLinkGenerated {
                WorkspaceExternalId = workspaceExternalId,
                FileExternalId = fileExternalId,
                ExpiresAt = expiresAt })
        };

        public class BulkDownloadLinkGenerated
        {
            public required string WorkspaceExternalId { get; init; }
            public required int SelectedFileCount { get; init; }
            public required int SelectedFolderCount { get; init; }
            public required DateTimeOffset ExpiresAt { get; init; }
        }

        public class SearchPerformed
        {
            public required List<string> WorkspaceExternalIds { get; init; }
            public required List<string> FolderExternalIds { get; init; }
            public required List<string> ExcludeWorkspaceExternalIds { get; init; }
            public required List<string> ExcludeFolderExternalIds { get; init; }
            public required List<string> Types { get; init; }
            public required List<string> NameContains { get; init; }
            public required List<string> Extensions { get; init; }
            public required List<string> ContentTypes { get; init; }
            public required DateTimeOffset? CreatedAfter { get; init; }
            public required DateTimeOffset? CreatedBefore { get; init; }
            public required long? SizeMin { get; init; }
            public required long? SizeMax { get; init; }
            public required int ResultCount { get; init; }
        }

        public static AuditLogEntry BulkDownloadLinkGeneratedEntry(
            AuditLogActorContext actor,
            string workspaceExternalId,
            int selectedFileCount,
            int selectedFolderCount,
            DateTimeOffset expiresAt) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Agent,
            EventType = AuditLogEventTypes.Agent.BulkDownloadLinkGenerated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new BulkDownloadLinkGenerated {
                WorkspaceExternalId = workspaceExternalId,
                SelectedFileCount = selectedFileCount,
                SelectedFolderCount = selectedFolderCount,
                ExpiresAt = expiresAt })
        };

        public static AuditLogEntry SearchPerformedEntry(
            AuditLogActorContext actor,
            List<string> workspaceExternalIds,
            List<string> folderExternalIds,
            List<string> excludeWorkspaceExternalIds,
            List<string> excludeFolderExternalIds,
            List<string> types,
            List<string> nameContains,
            List<string> extensions,
            List<string> contentTypes,
            DateTimeOffset? createdAfter,
            DateTimeOffset? createdBefore,
            long? sizeMin,
            long? sizeMax,
            int resultCount) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Agent,
            EventType = AuditLogEventTypes.Agent.SearchPerformed,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new SearchPerformed {
                WorkspaceExternalIds = workspaceExternalIds,
                FolderExternalIds = folderExternalIds,
                ExcludeWorkspaceExternalIds = excludeWorkspaceExternalIds,
                ExcludeFolderExternalIds = excludeFolderExternalIds,
                Types = types,
                NameContains = nameContains,
                Extensions = extensions,
                ContentTypes = contentTypes,
                CreatedAfter = createdAfter,
                CreatedBefore = createdBefore,
                SizeMin = sizeMin,
                SizeMax = sizeMax,
                ResultCount = resultCount })
        };

        public static AuditLogEntry FileViewedEntry(
            AuditLogActorContext actor,
            string workspaceExternalId,
            string fileExternalId) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Agent,
            EventType = AuditLogEventTypes.Agent.FileViewed,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new FileViewed {
                WorkspaceExternalId = workspaceExternalId,
                FileExternalId = fileExternalId })
        };

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
