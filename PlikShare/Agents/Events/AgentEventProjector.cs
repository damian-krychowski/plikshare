using PlikShare.Core.Clock;
using Serilog;

namespace PlikShare.Agents.Events;

/// <summary>
/// Drains the agent-event channel off the request thread, mirroring how <c>AuditLogWriter</c> drains
/// the audit channel. For each event it resolves the recipient agents and fans the event out into
/// their inboxes in the isolated events database. Periodically purges aged-out events so the inbox
/// stays bounded without ever touching the main database.
/// </summary>
public class AgentEventProjector(
    AgentEventChannel channel,
    AgentEventRecipientResolver recipientResolver,
    AgentEventInbox inbox,
    IClock clock) : BackgroundService
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<AgentEventProjector>();

    private static readonly TimeSpan PurgeInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan RetentionWindow = TimeSpan.FromDays(7);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Logger.Information("AgentEventProjector started.");

        var lastPurgeAt = clock.UtcNow;

        try
        {
            while (await channel.Reader.WaitToReadAsync(stoppingToken))
            {
                while (channel.Reader.TryRead(out var notification))
                {
                    try
                    {
                        await ProjectOne(notification, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex,
                            "Failed to project agent event: {EventType}",
                            notification.EventType);
                    }
                }

                if (clock.UtcNow - lastPurgeAt >= PurgeInterval)
                {
                    await PurgeExpired(stoppingToken);
                    lastPurgeAt = clock.UtcNow;
                }
            }
        }
        catch (OperationCanceledException)
        {
            Logger.Information("AgentEventProjector stopping due to cancellation.");
        }

        Logger.Information("AgentEventProjector stopped.");
    }

    private async Task ProjectOne(AgentEventNotification notification, CancellationToken cancellationToken)
    {
        var recipients = recipientResolver.Resolve(notification);

        if (recipients.Count == 0)
            return;

        await inbox.AppendForRecipients(notification, recipients, cancellationToken);
    }

    private async Task PurgeExpired(CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await inbox.PurgeOlderThan(
                clock.UtcNow - RetentionWindow,
                cancellationToken);

            if (deleted > 0)
                Logger.Information("AgentEventProjector purged {DeletedCount} expired agent events.", deleted);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to purge expired agent events.");
        }
    }
}
