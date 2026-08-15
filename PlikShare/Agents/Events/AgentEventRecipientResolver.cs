using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Core.UserIdentity;

namespace PlikShare.Agents.Events;

/// <summary>
/// Resolves, for one event, the set of enabled agents that should receive it. There is no event-type
/// filtering here — every event is eligible. An agent becomes a recipient only when it has a
/// legitimate basis to see the event and has not muted its category:
///   - scoped events (workspace/box): agents with access to that workspace or box,
///   - scopeless/system events: agents owned by the user behind the event's actor.
/// The agent that caused the event is never notified about its own action. Read against the main
/// database; the events database never joins to it.
/// </summary>
public class AgentEventRecipientResolver(PlikShareDb plikShareDb)
{
    public List<int> Resolve(AgentEventNotification notification)
    {
        using var connection = plikShareDb.OpenConnection();

        var hasScope = notification.WorkspaceExternalId is not null
                       || notification.BoxExternalId is not null;

        return hasScope
            ? ResolveScoped(connection, notification)
            : ResolveScopeless(connection, notification);
    }

    private static List<int> ResolveScoped(
        SqliteConnection connection,
        AgentEventNotification notification)
    {
        var actorAgentExternalId = notification.ActorIdentityType == AgentIdentity.Type
            ? notification.ActorIdentity
            : null;

        return connection
            .Cmd(
                sql: """
                     SELECT a.a_id
                     FROM a_agents a
                     WHERE a.a_is_enabled = TRUE
                       AND (
                           ($workspaceExternalId IS NOT NULL AND EXISTS (
                               SELECT 1
                               FROM w_workspaces w
                               WHERE w.w_external_id = $workspaceExternalId
                                 AND w.w_is_being_deleted = FALSE
                                 AND (
                                     w.w_owner_agent_id = a.a_id
                                     OR EXISTS (
                                         SELECT 1 FROM wa_workspace_agents wa
                                         WHERE wa.wa_workspace_id = w.w_id
                                           AND wa.wa_agent_id = a.a_id
                                     )
                                 )
                           ))
                           OR
                           ($boxExternalId IS NOT NULL AND EXISTS (
                               SELECT 1
                               FROM ba_box_agents ba
                               INNER JOIN bo_boxes bo ON bo.bo_id = ba.ba_box_id
                               WHERE bo.bo_external_id = $boxExternalId
                                 AND bo.bo_is_being_deleted = FALSE
                                 AND ba.ba_agent_id = a.a_id
                           ))
                       )
                       AND NOT EXISTS (
                           SELECT 1 FROM aes_agent_event_subscriptions s
                           WHERE s.aes_agent_id = a.a_id
                             AND s.aes_event_category = $eventCategory
                             AND s.aes_is_enabled = FALSE
                       )
                       AND (a.a_external_id != $actorAgentExternalId OR $actorAgentExternalId IS NULL)
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$workspaceExternalId", notification.WorkspaceExternalId)
            .WithParameter("$boxExternalId", notification.BoxExternalId)
            .WithParameter("$eventCategory", notification.EventCategory)
            .WithParameter("$actorAgentExternalId", actorAgentExternalId)
            .Execute();
    }

    private static List<int> ResolveScopeless(
        SqliteConnection connection,
        AgentEventNotification notification)
    {
        var ownerUserId = ResolveActorOwnerUserId(connection, notification);

        if (ownerUserId is null)
            return [];

        var actorAgentExternalId = notification.ActorIdentityType == AgentIdentity.Type
            ? notification.ActorIdentity
            : null;

        return connection
            .Cmd(
                sql: """
                     SELECT a.a_id
                     FROM a_agents a
                     WHERE a.a_is_enabled = TRUE
                       AND a.a_owner_user_id = $ownerUserId
                       AND NOT EXISTS (
                           SELECT 1 FROM aes_agent_event_subscriptions s
                           WHERE s.aes_agent_id = a.a_id
                             AND s.aes_event_category = $eventCategory
                             AND s.aes_is_enabled = FALSE
                       )
                       AND (a.a_external_id != $actorAgentExternalId OR $actorAgentExternalId IS NULL)
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$ownerUserId", ownerUserId.Value)
            .WithParameter("$eventCategory", notification.EventCategory)
            .WithParameter("$actorAgentExternalId", actorAgentExternalId)
            .Execute();
    }

    /// <summary>
    /// The user behind the actor: the actor user itself, or — when an agent acted — that agent's
    /// owner. Anonymous and session actors have no owning user, so their scopeless events reach no one.
    /// </summary>
    private static int? ResolveActorOwnerUserId(
        SqliteConnection connection,
        AgentEventNotification notification)
    {
        if (notification.ActorIdentityType == UserIdentity.Type)
        {
            var result = connection
                .OneRowCmd(
                    sql: "SELECT u_id FROM u_users WHERE u_external_id = $externalId",
                    readRowFunc: reader => reader.GetInt32(0))
                .WithParameter("$externalId", notification.ActorIdentity)
                .Execute();

            return result.IsEmpty ? null : result.Value;
        }

        if (notification.ActorIdentityType == AgentIdentity.Type)
        {
            var result = connection
                .OneRowCmd(
                    sql: "SELECT a_owner_user_id FROM a_agents WHERE a_external_id = $externalId",
                    readRowFunc: reader => reader.GetInt32(0))
                .WithParameter("$externalId", notification.ActorIdentity)
                .Execute();

            return result.IsEmpty ? null : result.Value;
        }

        return null;
    }
}
