using PlikShare.Core.Clock;
using Serilog;

namespace PlikShare.Agents.Operations;

/// <summary>
/// Periodic background sweep over the agent-operations ledger. Each tick (1) expires pending
/// operations whose approval window has elapsed and (2) purges resolved operations older than the
/// retention window — keeping the table, and any stored result payloads, bounded over time.
///
/// <para>A scanner over the whole table (rather than a per-operation scheduled job) means an
/// admin shortening retention takes effect on the next tick, a burst of operations doesn't
/// enqueue a burst of cleanup jobs, and an offline period simply catches up at the next tick.</para>
/// </summary>
public class AgentOperationsSweeperHostedService(
    AgentOperationLedger operationLedger,
    IClock clock,
    IHostApplicationLifetime lifetime,
    AgentOperationsOptions options) : BackgroundService
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<AgentOperationsSweeperHostedService>();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Logger.Information(
            "AgentOperationsSweeperHostedService started. Interval: {IntervalSeconds}s, retention: {RetentionHours}h",
            options.IntervalSeconds,
            options.RetentionHours);

        try
        {
            await WaitForApplicationStarted(stoppingToken);

            if (options.StartupGraceSeconds > 0)
                await Task.Delay(
                    TimeSpan.FromSeconds(options.StartupGraceSeconds),
                    stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunTick(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "AgentOperationsSweeper tick failed");
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(options.IntervalSeconds),
                    stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        Logger.Information("AgentOperationsSweeperHostedService stopped.");
    }

    private async Task WaitForApplicationStarted(CancellationToken stoppingToken)
    {
        var hostStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        await using var registration = lifetime
            .ApplicationStarted
            .Register(() => hostStarted.TrySetResult());

        await hostStarted.Task.WaitAsync(stoppingToken);
    }

    // internal (not private) so integration tests can drive a single deterministic sweep
    // instead of waiting out the production interval.
    internal async Task RunTick(CancellationToken cancellationToken)
    {
        var expired = await operationLedger.ExpirePending(cancellationToken);

        var purged = await operationLedger.PurgeResolvedOlderThan(
            createdBefore: clock.UtcNow.AddHours(-options.RetentionHours),
            cancellationToken: cancellationToken);

        if (expired > 0 || purged > 0)
            Logger.Information(
                "AgentOperationsSweeper tick: expired {Expired}, purged {Purged}",
                expired,
                purged);
    }
}

public class AgentOperationsOptions
{
    // How often the sweeper ticks.
    public int IntervalSeconds { get; init; } = 600;

    // How long an operation stays pending before its approval window lapses. An agent will not
    // poll for hours — once this passes the request is stale.
    public int ApprovalWindowHours { get; init; } = 2;

    // How long resolved operations (and any stored result) are retained before being purged.
    public int RetentionHours { get; init; } = 6;

    public int StartupGraceSeconds { get; init; } = 5;
}
