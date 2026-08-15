using PlikShare.Agents.Events.Id;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.EventsDatabase;
using PlikShare.Core.SQLite;

namespace PlikShare.Agents.Events;

/// <summary>
/// The per-agent event inbox, living in the isolated events database. Writes go through the events
/// write queue (single-writer serialization, off the main database's lane); reads open their own
/// connection. <c>aev_id</c> is the monotonic per-agent cursor the poll tool walks.
/// </summary>
public class AgentEventInbox(
    EventsDbWriteQueue eventsDbWriteQueue,
    PlikShareEventsDb plikShareEventsDb,
    IClock clock)
{
    /// <summary>
    /// Appends one event for every recipient agent in a single write-queue turn.
    /// </summary>
    public Task AppendForRecipients(
        AgentEventNotification notification,
        IReadOnlyList<int> recipientAgentIds,
        CancellationToken cancellationToken)
    {
        if (recipientAgentIds.Count == 0)
            return Task.CompletedTask;

        var createdAt = clock.UtcNow;

        return eventsDbWriteQueue.Execute(
            operationToEnqueue: context =>
            {
                foreach (var agentId in recipientAgentIds)
                {
                    context.Connection
                        .NonQueryCmd(
                            sql: """
                                 INSERT INTO aev_agent_events (
                                     aev_external_id,
                                     aev_agent_id,
                                     aev_event_type,
                                     aev_event_category,
                                     aev_workspace_external_id,
                                     aev_box_external_id,
                                     aev_actor_identity_type,
                                     aev_actor_identity,
                                     aev_occurred_at,
                                     aev_created_at,
                                     aev_is_acked
                                 ) VALUES (
                                     $externalId,
                                     $agentId,
                                     $eventType,
                                     $eventCategory,
                                     $workspaceExternalId,
                                     $boxExternalId,
                                     $actorIdentityType,
                                     $actorIdentity,
                                     $occurredAt,
                                     $createdAt,
                                     FALSE
                                 )
                                 """)
                        .WithParameter("$externalId", AgentEventExtId.NewId().Value)
                        .WithParameter("$agentId", agentId)
                        .WithParameter("$eventType", notification.EventType)
                        .WithParameter("$eventCategory", notification.EventCategory)
                        .WithParameter("$workspaceExternalId", notification.WorkspaceExternalId)
                        .WithParameter("$boxExternalId", notification.BoxExternalId)
                        .WithParameter("$actorIdentityType", notification.ActorIdentityType)
                        .WithParameter("$actorIdentity", notification.ActorIdentity)
                        .WithParameter("$occurredAt", notification.OccurredAt)
                        .WithParameter("$createdAt", createdAt)
                        .Execute();
                }
            },
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// The agent's events newer than the given cursor, oldest first. <paramref name="afterCursor"/> of
    /// 0 starts from the agent's oldest retained event.
    /// </summary>
    public List<AgentEventRecord> ListSince(int agentId, long afterCursor, int limit)
    {
        using var connection = plikShareEventsDb.OpenConnection();

        return connection
            .Cmd(
                sql: """
                     SELECT
                         aev_id,
                         aev_external_id,
                         aev_event_type,
                         aev_event_category,
                         aev_workspace_external_id,
                         aev_box_external_id,
                         aev_actor_identity_type,
                         aev_actor_identity,
                         aev_occurred_at
                     FROM aev_agent_events
                     WHERE aev_agent_id = $agentId
                       AND aev_id > $afterCursor
                     ORDER BY aev_id
                     LIMIT $limit
                     """,
                readRowFunc: reader => new AgentEventRecord(
                    Cursor: reader.GetInt64(0),
                    ExternalId: reader.GetString(1),
                    EventType: reader.GetString(2),
                    EventCategory: reader.GetString(3),
                    WorkspaceExternalId: reader.GetStringOrNull(4),
                    BoxExternalId: reader.GetStringOrNull(5),
                    ActorIdentityType: reader.GetString(6),
                    ActorIdentity: reader.GetString(7),
                    OccurredAt: reader.GetFieldValue<DateTimeOffset>(8)))
            .WithParameter("$agentId", agentId)
            .WithParameter("$afterCursor", afterCursor)
            .WithParameter("$limit", limit)
            .Execute();
    }

    /// <summary>
    /// Marks the agent's events up to and including the cursor as acknowledged, so retention can drop
    /// them. Returns the number newly acknowledged.
    /// </summary>
    public Task<int> Acknowledge(int agentId, long throughCursor, CancellationToken cancellationToken)
    {
        return eventsDbWriteQueue.Execute(
            operationToEnqueue: context => context.Connection
                .NonQueryCmd(
                    sql: """
                         UPDATE aev_agent_events
                         SET aev_is_acked = TRUE
                         WHERE aev_agent_id = $agentId
                           AND aev_id <= $throughCursor
                           AND aev_is_acked = FALSE
                         """)
                .WithParameter("$agentId", agentId)
                .WithParameter("$throughCursor", throughCursor)
                .Execute()
                .AffectedRows,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Permanently deletes events created before the cutoff, bounding how long the inbox is retained.
    /// Returns the number deleted.
    /// </summary>
    public Task<int> PurgeOlderThan(DateTimeOffset createdBefore, CancellationToken cancellationToken)
    {
        // Cheap read first — never occupy the single-writer queue with a no-op DELETE.
        using (var connection = plikShareEventsDb.OpenConnection())
        {
            var hasOld = connection
                .OneRowCmd(
                    sql: "SELECT 1 FROM aev_agent_events WHERE aev_created_at < $createdBefore LIMIT 1",
                    readRowFunc: reader => reader.GetInt32(0))
                .WithParameter("$createdBefore", createdBefore)
                .Execute();

            if (hasOld.IsEmpty)
                return Task.FromResult(0);
        }

        return eventsDbWriteQueue.Execute(
            operationToEnqueue: context => context.Connection
                .NonQueryCmd(
                    sql: "DELETE FROM aev_agent_events WHERE aev_created_at < $createdBefore")
                .WithParameter("$createdBefore", createdBefore)
                .Execute()
                .AffectedRows,
            cancellationToken: cancellationToken);
    }
}

public sealed record AgentEventRecord(
    long Cursor,
    string ExternalId,
    string EventType,
    string EventCategory,
    string? WorkspaceExternalId,
    string? BoxExternalId,
    string ActorIdentityType,
    string ActorIdentity,
    DateTimeOffset OccurredAt);
