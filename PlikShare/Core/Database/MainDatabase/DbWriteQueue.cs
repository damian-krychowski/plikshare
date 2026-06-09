using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Data.Sqlite;
using PlikShare.Core.Configuration;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.Core.Database.MainDatabase;

/// <summary>
/// this queue allows to serialize all hot path sqlite db writes. its important because SQLite support only one writer at the time
/// so even if someone will create separate connection to sqlite db below the surface they will be waiting for each other
/// which is much less efficient than create a single thread loop to run through all write operations. This way writes are not creating
/// separate connections and they can reuse the same SQLiteCommands which is quite a performance boost.
///
/// Writes are split into priority lanes (see <see cref="DbWritePriority"/>): UI/foreground first, then job
/// writes by their job priority. The single writer always picks the highest-priority lane, except that a
/// write waiting past its lane's anti-starvation budget jumps ahead once — so a steady stream of UI writes
/// cannot indefinitely starve job completion writes. The lane is taken from an ambient
/// <see cref="DbWritePriorityScope"/> (set by the queue consumers for the duration of a job), so every write
/// a job makes — both in its Execute phase and its completion write — inherits the job's priority without the
/// 160+ call sites having to pass it explicitly. UI request paths have no ambient scope and default to Ui.
/// </summary>
public class DbWriteQueue : IDisposable
{
    private const string QueueName = "main";
    private static readonly TimeSpan IdlePollInterval = TimeSpan.FromSeconds(1);
    private static readonly int LaneCount = Enum.GetValues<DbWritePriority>().Length;

    private static readonly Serilog.ILogger Logger = Log.ForContext<DbWriteQueue>();

    private readonly PlikShareDb _plikShareDb;
    private readonly SqliteWriteQueueMetrics _metrics;

    private readonly object _queueLock = new();
    private volatile bool _isProcessing;

    private readonly Queue<IDbWriteOperation>[] _lanes;
    private readonly long[] _laneMaxWaitTicks;
    private int _queuedCount;

    private readonly CancellationTokenSource _cancellationSource = new();

    private Task? _processTask;

    public DbWriteQueue(
        PlikShareDb plikShareDb,
        SqliteWriteQueueMetrics metrics,
        IConfig config)
    {
        _plikShareDb = plikShareDb;
        _metrics = metrics;

        _lanes = new Queue<IDbWriteOperation>[LaneCount];
        _laneMaxWaitTicks = new long[LaneCount];

        for (var i = 0; i < LaneCount; i++)
        {
            _lanes[i] = new Queue<IDbWriteOperation>();

            var maxWait = i < config.DbWritePriorityMaxWaits.Count
                ? config.DbWritePriorityMaxWaits[i]
                : TimeSpan.Zero;

            _laneMaxWaitTicks[i] = (long)(maxWait.TotalSeconds * Stopwatch.Frequency);
        }
    }

    public Task Execute(
        Action<SqliteWriteContext> operationToEnqueue,
        CancellationToken cancellationToken,
        DbWritePriority? priority = null,
        [CallerFilePath] string? callerFilePath = null,
        [CallerMemberName] string? callerMember = null)
    {
        var lane = priority ?? DbWritePriorityScope.Effective;

        var dbWriteRequest = new DbWriteOperation(
            operation: operationToEnqueue,
            source: SqliteWriteQueueMetrics.BuildSource(callerFilePath, callerMember),
            enqueuedTimestamp: Stopwatch.GetTimestamp(),
            priority: lane);

        Enqueue(dbWriteRequest, lane, cancellationToken);

        return dbWriteRequest.CompletionSource.Task;
    }

    public Task<TResult> Execute<TResult>(
        Func<SqliteWriteContext, TResult> operationToEnqueue,
        CancellationToken cancellationToken,
        DbWritePriority? priority = null,
        [CallerFilePath] string? callerFilePath = null,
        [CallerMemberName] string? callerMember = null)
    {
        var lane = priority ?? DbWritePriorityScope.Effective;

        var dbWriteRequest = new DbWriteOperation<TResult>(
            operation: operationToEnqueue,
            source: SqliteWriteQueueMetrics.BuildSource(callerFilePath, callerMember),
            enqueuedTimestamp: Stopwatch.GetTimestamp(),
            priority: lane);

        Enqueue(dbWriteRequest, lane, cancellationToken);

        return dbWriteRequest.CompletionSource.Task;
    }

    private void Enqueue(
        IDbWriteOperation operation,
        DbWritePriority priority,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        int depthAtEnqueue;

        lock (_queueLock)
        {
            depthAtEnqueue = _queuedCount;

            _lanes[(int)priority].Enqueue(operation);
            _queuedCount++;

            Monitor.Pulse(_queueLock);
        }

        _metrics.RecordEnqueue(QueueName, depthAtEnqueue, priority);

        EnsureProcessingStarted();
    }

    private void EnsureProcessingStarted()
    {
        if (_isProcessing)
            return;

        lock (_queueLock)
        {
            if (_isProcessing) return;

            _isProcessing = true;
            _processTask = Task.Run(Process);

            Logger.Debug("{QueueName} queue started", nameof(DbWriteQueue));
        }
    }

    private void Process()
    {
        _metrics.RegisterQueue(QueueName, () => Volatile.Read(ref _queuedCount));

        using var connection = _plikShareDb.OpenConnection();
        using var commandsPool = connection.CreateLazyCommandsPool();

        var context = new SqliteWriteContext
        {
            Connection = connection,
            CommandsPool = commandsPool
        };

        while (!_cancellationSource.Token.IsCancellationRequested)
        {
            try
            {
                IDbWriteOperation? dbWriteOperation;

                lock (_queueLock)
                {
                    if (_queuedCount == 0)
                    {
                        Monitor.Wait(_queueLock, IdlePollInterval);

                        if (_queuedCount == 0)
                        {
                            _isProcessing = false;
                            Logger.Debug("{QueueName} queue stopped due to inactivity", nameof(DbWriteQueue));
                            return;
                        }
                    }

                    dbWriteOperation = DequeueByPolicy(
                        Stopwatch.GetTimestamp());
                        
                    _queuedCount--;
                }

                var queueWaitMs = Stopwatch
                    .GetElapsedTime(dbWriteOperation.EnqueuedTimestamp)
                    .TotalMilliseconds;

                var executionStart = Stopwatch.GetTimestamp();

                try
                {
                    switch (dbWriteOperation)
                    {
                        case ISyncDbWriteOperation syncDbWriteOperation:
                            syncDbWriteOperation.Execute(
                                context);
                            break;

                        default:
                            throw new ArgumentOutOfRangeException(nameof(dbWriteOperation));
                    }
                }
                finally
                {
                    _metrics.RecordCompleted(
                        queue: QueueName,
                        source: dbWriteOperation.Source,
                        queueWaitMs: queueWaitMs,
                        executionMs: Stopwatch.GetElapsedTime(executionStart).TotalMilliseconds,
                        success: !dbWriteOperation.HasFaulted,
                        lane: dbWriteOperation.Priority);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error in conversion queue processing loop");
                // Continue processing - we don't want to stop the queue
            }
        }
    }

    // Caller holds _queueLock and guarantees _queuedCount > 0. Strict priority: serve the highest-priority
    // (lowest index) non-empty lane. Anti-starvation override: if any lower-priority lane's head has waited
    // past its lane budget, serve the oldest such head instead, so a steady higher-priority stream can't
    // starve lower lanes indefinitely.
    private IDbWriteOperation DequeueByPolicy(long now)
    {
        var strict = -1;

        for (var i = 0; i < LaneCount; i++)
        {
            if (_lanes[i].Count > 0)
            {
                strict = i;
                break;
            }
        }

        var chosen = strict;
        var oldestStarvedTimestamp = long.MaxValue;

        for (var i = strict + 1; i < LaneCount; i++)
        {
            var lane = _lanes[i];

            if (lane.Count == 0)
                continue;

            var head = lane.Peek();

            if (now - head.EnqueuedTimestamp >= _laneMaxWaitTicks[i]
                && head.EnqueuedTimestamp < oldestStarvedTimestamp)
            {
                chosen = i;
                oldestStarvedTimestamp = head.EnqueuedTimestamp;
            }
        }

        return _lanes[chosen].Dequeue();
    }

    public void Dispose()
    {
        _cancellationSource.Cancel();

        lock (_queueLock)
        {
            Monitor.PulseAll(_queueLock);
        }

        _processTask?.Wait(TimeSpan.FromSeconds(5));
        _cancellationSource.Dispose();
    }

    private interface IDbWriteOperation
    {
        string Source { get; }
        long EnqueuedTimestamp { get; }
        DbWritePriority Priority { get; }
        bool HasFaulted { get; }
    }

    private interface ISyncDbWriteOperation : IDbWriteOperation
    {
        void Execute(SqliteWriteContext context);
    }

    private class DbWriteOperation<TResult>(
        Func<SqliteWriteContext, TResult> operation,
        string source,
        long enqueuedTimestamp,
        DbWritePriority priority) : ISyncDbWriteOperation
    {
        public readonly TaskCompletionSource<TResult> CompletionSource = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public string Source { get; } = source;
        public long EnqueuedTimestamp { get; } = enqueuedTimestamp;
        public DbWritePriority Priority { get; } = priority;
        public bool HasFaulted { get; private set; }

        public void Execute(SqliteWriteContext context)
        {
            try
            {
                var result = operation(context);
                CompletionSource.SetResult(result);
            }
            catch (Exception e)
            {
                HasFaulted = true;
                CompletionSource.SetException(e);
            }
        }
    }

    private class DbWriteOperation(
        Action<SqliteWriteContext> operation,
        string source,
        long enqueuedTimestamp,
        DbWritePriority priority) : ISyncDbWriteOperation
    {
        public readonly TaskCompletionSource CompletionSource = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public string Source { get; } = source;
        public long EnqueuedTimestamp { get; } = enqueuedTimestamp;
        public DbWritePriority Priority { get; } = priority;
        public bool HasFaulted { get; private set; }

        public void Execute(SqliteWriteContext context)
        {
            try
            {
                operation(context);
                CompletionSource.SetResult();
            }
            catch (Exception e)
            {
                HasFaulted = true;
                CompletionSource.SetException(e);
            }
        }
    }
}

public static class DbWriteQueueContextExtensions
{
    public static void DeferForeignKeys(
        this SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction)
    {
        dbWriteContext
            .Connection
            .NonQueryCmd(
                sql: "PRAGMA defer_foreign_keys = ON;",
                transaction: transaction)
            .Execute();
    }
}
