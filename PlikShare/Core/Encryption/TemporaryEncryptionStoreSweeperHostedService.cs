using Serilog;

namespace PlikShare.Core.Encryption;

/// <summary>
/// Periodic background sweep that drops expired entries from
/// <see cref="TemporaryEncryptionStore"/>. Pure in-memory work — no DB, no I/O, no
/// per-entry resources to release; each tick just calls <see cref="TemporaryEncryptionStore.SweepExpired"/>.
///
/// <para>Without this loop expired entries linger as long as something accidentally keeps the
/// dictionary slot warm. <see cref="TemporaryEncryptionStore.TryRetrieve"/> already
/// purges on-demand for any handle that gets looked up, but never-retried-after-failure
/// entries (queue job vanished, worker crashed mid-flight) need this background pass to
/// disappear after their 24-hour TTL elapses.</para>
///
/// <para>Cancellation/restart loses every entry — by design, the store is process-scoped.</para>
/// </summary>
public class TemporaryEncryptionStoreSweeperHostedService(
    TemporaryEncryptionStore store,
    IHostApplicationLifetime lifetime,
    TemporaryEncryptionStoreSweeperOptions options) : BackgroundService
{
    private static readonly Serilog.ILogger Logger =
        Log.ForContext<TemporaryEncryptionStoreSweeperHostedService>();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Logger.Information(
            "TemporaryEncryptionStoreSweeperHostedService started. Interval: {IntervalSeconds}s",
            options.IntervalSeconds);

        // Hold off until the host has fully started so the first sweep doesn't compete with the
        // startup burst, then add a short grace period for the same reason. Same pattern as
        // TrashSweeperHostedService — keeps the lifecycle deterministic.
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
                RunTick();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                // A failed tick must not kill the loop. Sweeping is best-effort — next tick
                // will retry naturally.
                Logger.Error(ex, "TemporaryEncryptionStoreSweeper tick failed");
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

        Logger.Information("TemporaryEncryptionStoreSweeperHostedService stopped.");
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
    // instead of waiting out the production interval. The background loop above is the
    // only other caller.
    internal void RunTick()
    {
        store.SweepExpired();
    }
}

public class TemporaryEncryptionStoreSweeperOptions
{
    /// <summary>
    /// How often the sweeper runs. With <see cref="TemporaryEncryptionStore.DefaultTtl"/>
    /// at 24h, an expired entry lives at most TTL + this interval before being removed. 5 minutes
    /// is a reasonable default — the store is in-memory so memory pressure is the only cost, and
    /// each entry is on the order of bytes per wire.
    /// </summary>
    public int IntervalSeconds { get; init; } = 300;

    /// <summary>
    /// Grace period after the host has fully started, before the first sweep. Keeps the first
    /// pass from running during startup. Set to 0 to sweep immediately after host start.
    /// </summary>
    public int StartupGraceSeconds { get; init; } = 5;
}
