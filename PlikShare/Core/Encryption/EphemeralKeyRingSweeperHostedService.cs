using Serilog;

namespace PlikShare.Core.Encryption;

public class EphemeralKeyRingSweeperHostedService(
    EphemeralKeyRing keyRing,
    EphemeralKeyRingOptions options,
    IHostApplicationLifetime lifetime) : BackgroundService
{
    private static readonly Serilog.ILogger Logger =
        Log.ForContext<EphemeralKeyRingSweeperHostedService>();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Logger.Information(
            "EphemeralKeyRingSweeperHostedService started. Interval: {IntervalSeconds}s",
            options.SweepIntervalSeconds);

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
                Logger.Error(ex, "EphemeralKeyRingSweeper tick failed");
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(options.SweepIntervalSeconds),
                    stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        Logger.Information("EphemeralKeyRingSweeperHostedService stopped.");
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

    internal void RunTick()
    {
        keyRing.SweepExpired();
    }
}
