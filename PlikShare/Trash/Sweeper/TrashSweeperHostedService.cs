using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Trash.Sweeper;

/// <summary>
/// Periodic background sweep that permanently deletes trashed files whose retention window
/// has elapsed. Each tick scans workspaces holding soft-deleted files, resolves each one's
/// retention (disabled policy → 0 days, so leftover trash is purged; enabled + N days → N;
/// enabled + no limit → skipped), then hands the expired file IDs to
/// <see cref="PurgeFilesSubQuery"/> for normal hard-delete + storage purge job emission.
///
/// <para>Scanner over per-item scheduled jobs is intentional: shortening a workspace's
/// retention (30 → 7 days) takes effect on the next tick without needing to find and reschedule
/// thousands of pending queue rows; a bulk-delete of 10k files doesn't put 10k jobs in the
/// queue; an offline period of days just catches up at the next tick.</para>
///
/// <para>Per-workspace cap (<see cref="TrashSweeperOptions.MaxItemsPerWorkspacePerTick"/>)
/// keeps a single workspace's huge trash backlog from starving everyone else — leftovers
/// are picked up on subsequent ticks.</para>
/// </summary>
public class TrashSweeperHostedService(
    PlikShareDb plikShareDb,
    DbWriteQueue dbWriteQueue,
    PurgeFilesSubQuery purgeFilesSubQuery,
    WorkspaceCache workspaceCache,
    IClock clock,
    IHostApplicationLifetime lifetime,
    TrashSweeperOptions options) : BackgroundService
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<TrashSweeperHostedService>();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Logger.Information(
            "TrashSweeperHostedService started. Interval: {IntervalSeconds}s, cap per workspace: {Cap}",
            options.IntervalSeconds,
            options.MaxItemsPerWorkspacePerTick);

        // Hold off the first scan until the host has fully started (all hosted services started,
        // Kestrel listening) — a deterministic lifecycle signal instead of guessing with a fixed
        // delay. Plus a short grace period so the first DB scan doesn't compete with the startup
        // burst (queue producer polling, consumers spinning up).
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
                // A failed tick must not kill the loop. Surface it loudly and continue —
                // next tick will retry naturally.
                Logger.Error(ex, "TrashSweeper tick failed");
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

        Logger.Information("TrashSweeperHostedService stopped.");
    }

    // Completes once IHostApplicationLifetime.ApplicationStarted fires; throws OperationCanceledException
    // if the service is stopped first. Safe from deadlock: ExecuteAsync yields at the first await,
    // so StartAsync returns, the host finishes starting, and the token fires.
    private async Task WaitForApplicationStarted(CancellationToken stoppingToken)
    {
        var hostStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        await using var registration = lifetime
            .ApplicationStarted
            .Register(() => hostStarted.TrySetResult());

        await hostStarted.Task.WaitAsync(stoppingToken);
    }

    private async Task RunTick(CancellationToken cancellationToken)
    {
        var workspaceIds = GetSweepCandidates();

        if (workspaceIds.Count == 0)
            return;

        Logger.Debug("TrashSweeper tick: {Count} workspaces with trash to sweep",
            workspaceIds.Count);

        foreach (var workspaceId in workspaceIds)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                await SweepWorkspace(workspaceId, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.Error(ex,
                    "TrashSweeper failed for Workspace#{WorkspaceId}",
                    workspaceId);
            }
        }
    }

    /// <summary>
    /// Step 1: every workspace that currently holds at least one soft-deleted file. The purge
    /// decision is made from the parsed policy — not filtered in SQL — because a disabled
    /// policy still has to purge its leftover trash. Workspaces whose policy keeps trash
    /// forever (enabled, retentionDays null) are dropped here so they never reach the purge step.
    /// </summary>
    private List<int> GetSweepCandidates()
    {
        using var connection = plikShareDb.OpenConnection();

        var rows = connection
            .Cmd(
                sql: """
                     SELECT w.w_id, w.w_trash_policy_json
                     FROM w_workspaces AS w
                     WHERE w.w_is_being_deleted = FALSE
                       AND EXISTS (
                           SELECT 1
                           FROM fi_files fi
                           WHERE fi.fi_workspace_id = w.w_id
                             AND fi.fi_deleted_at IS NOT NULL
                       )
                     """,
                readRowFunc: reader => new
                {
                    WorkspaceId = reader.GetInt32(0),
                    Policy = reader.GetFromJson<TrashPolicy>(1)
                })
            .Execute();

        return rows
            .Where(r => ShouldPurgeDeletedFiles(r.Policy))
            .Select(r => r.WorkspaceId)
            .ToList();
    }

    private static bool ShouldPurgeDeletedFiles(TrashPolicy policy)
    {
        return policy is not { Enabled: true, RetentionDays: null };
    }

    // The moment before which trashed files count as expired. Caller must have confirmed
    // ShouldPurgeDeletedFiles first.
    private DateTimeOffset GetPurgeCutoff(TrashPolicy policy)
    {
        return !policy.Enabled 
            ? clock.UtcNow  // disabled → every leftover trashed file is expired
            : clock.UtcNow.AddDays(-policy.RetentionDays!.Value); // enabled → older than N days
    }

    private async Task SweepWorkspace(
        int workspaceId,
        CancellationToken cancellationToken)
    {
        var workspace = await workspaceCache.TryGetWorkspace(
            workspaceId: workspaceId,
            cancellationToken: cancellationToken);

        if (workspace is null || workspace.IsBeingDeleted)
            return;

        // Re-check against the fresh cached policy — an admin may have switched it to
        // "keep forever" between the scan and now.
        if (!ShouldPurgeDeletedFiles(workspace.TrashPolicy))
            return;

        var cutoff = GetPurgeCutoff(workspace.TrashPolicy);
        var cap = options.MaxItemsPerWorkspacePerTick;
        var correlationId = Guid.NewGuid();

        await dbWriteQueue.Execute(
            operationToEnqueue: ctx => PurgeExpired(
                ctx,
                workspace,
                cutoff,
                cap,
                correlationId),
            cancellationToken: cancellationToken);
    }

    private int PurgeExpired(
        SqliteWriteContext dbWriteContext,
        WorkspaceContext workspace,
        DateTimeOffset cutoff,
        int cap,
        Guid correlationId)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            dbWriteContext.DeferForeignKeys(transaction);

            var fileIds = dbWriteContext
                .Cmd(
                    sql: """
                         SELECT fi_id
                         FROM fi_files
                         WHERE fi_workspace_id = $workspaceId
                           AND fi_deleted_at IS NOT NULL
                           AND fi_deleted_at < $cutoff
                           AND fi_parent_file_id IS NULL
                         ORDER BY fi_deleted_at ASC
                         LIMIT $cap
                         """,
                    readRowFunc: reader => reader.GetInt32(0),
                    transaction: transaction)
                .WithParameter("$workspaceId", workspace.Id)
                .WithParameter("$cutoff", cutoff)
                .WithParameter("$cap", cap)
                .Execute();

            if (fileIds.Count == 0)
            {
                transaction.Commit();
                return 0;
            }

            var deletedCount = purgeFilesSubQuery.Execute(
                workspaceId: workspace.Id,
                fileIds: fileIds,
                correlationId: correlationId,
                dbWriteContext: dbWriteContext,
                transaction: transaction);

            transaction.Commit();

            Logger.Information(
                "TrashSweeper purged {Count} expired items in Workspace#{WorkspaceId} (cutoff: {Cutoff})",
                deletedCount,
                workspace.Id,
                cutoff);

            return deletedCount;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

}

public class TrashSweeperOptions
{
    public int IntervalSeconds { get; init; } = 3600;
    public int MaxItemsPerWorkspacePerTick { get; init; } = 5000;

    // Grace period after the host has fully started, before the first sweep. Keeps the first
    // DB scan from competing with the startup burst. Set to 0 to sweep immediately.
    public int StartupGraceSeconds { get; init; } = 5;
}
