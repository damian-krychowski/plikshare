using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Data.Sqlite;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.Core.Database.MainDatabase;

/// <summary>
/// this queue allows to serialize all hot path sqlite db writes. its important because SQLite support only one writer at the time
/// so even if someone will create separate connection to sqlite db below the surface they will be waiting for each other
/// which is much less efficient than create a single thread loop to run through all write operations. This way writes are not creating
/// separate connections and they can reuse the same SQLiteCommands which is quite a performance boost.
/// </summary>
public class DbWriteQueue(
    PlikShareDb plikShareDb,
    SqliteWriteQueueMetrics metrics) : IDisposable
{
    private const string QueueName = "main";

    private static readonly Serilog.ILogger Logger = Log.ForContext<DbWriteQueue>();

    private readonly Lock _processingLock = new();
    private volatile bool _isProcessing;

    private readonly BlockingCollection<IDbWriteOperation> _requestsQueue = new();
    private readonly CancellationTokenSource _cancellationSource = new();

    private Task? _processTask;

    public Task Execute(
        Action<SqliteWriteContext> operationToEnqueue,
        CancellationToken cancellationToken,
        [CallerFilePath] string? callerFilePath = null,
        [CallerMemberName] string? callerMember = null)
    {
        var dbWriteRequest = new DbWriteOperation(
            operation: operationToEnqueue,
            source: SqliteWriteQueueMetrics.BuildSource(callerFilePath, callerMember),
            enqueuedTimestamp: Stopwatch.GetTimestamp());

        metrics.RecordEnqueue(QueueName, _requestsQueue.Count);

        _requestsQueue.Add(
            item: dbWriteRequest,
            cancellationToken: cancellationToken);

        EnsureProcessingStarted();

        return dbWriteRequest.CompletionSource.Task;
    }

    public Task<TResult> Execute<TResult>(
        Func<SqliteWriteContext, TResult> operationToEnqueue,
        CancellationToken cancellationToken,
        [CallerFilePath] string? callerFilePath = null,
        [CallerMemberName] string? callerMember = null)
    {
        var dbWriteRequest = new DbWriteOperation<TResult>(
            operation: operationToEnqueue,
            source: SqliteWriteQueueMetrics.BuildSource(callerFilePath, callerMember),
            enqueuedTimestamp: Stopwatch.GetTimestamp());

        metrics.RecordEnqueue(QueueName, _requestsQueue.Count);

        _requestsQueue.Add(
            item: dbWriteRequest,
            cancellationToken: cancellationToken);

        EnsureProcessingStarted();

        return dbWriteRequest.CompletionSource.Task;
    }

    private void EnsureProcessingStarted()
    {
        if (_isProcessing)
            return;

        lock (_processingLock)
        {
            if (_isProcessing) return;

            _isProcessing = true;
            _processTask = Task.Run(Process);

            Logger.Debug("{QueueName} queue started", nameof(DbWriteQueue));
        }
    }

    private async Task Process()
    {
        metrics.RegisterQueue(QueueName, () => _requestsQueue.Count);

        using var connection = plikShareDb.OpenConnection();
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
                // Try to take first item with 1 second timeout
                if (!_requestsQueue.TryTake(out var dbWriteOperation, TimeSpan.FromSeconds(1)))
                {
                    lock (_processingLock)
                    {
                        if (_requestsQueue.Count == 0) // Double check queue is still empty
                        {
                            _isProcessing = false;
                            Logger.Debug("{QueueName} queue stopped due to inactivity", nameof(DbWriteQueue));
                            return;
                        }
                    }

                    continue;
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
                    metrics.RecordCompleted(
                        queue: QueueName,
                        source: dbWriteOperation.Source,
                        queueWaitMs: queueWaitMs,
                        executionMs: Stopwatch.GetElapsedTime(executionStart).TotalMilliseconds,
                        success: !dbWriteOperation.HasFaulted);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error in conversion queue processing loop");
                // Continue processing - we don't want to stop the queue
            }
        }
    }

    public void Dispose()
    {
        _cancellationSource.Cancel();
        _processTask?.Wait(TimeSpan.FromSeconds(5));
        _cancellationSource.Dispose();
    }

    private interface IDbWriteOperation
    {
        string Source { get; }
        long EnqueuedTimestamp { get; }
        bool HasFaulted { get; }
    }

    private interface ISyncDbWriteOperation : IDbWriteOperation
    {
        void Execute(SqliteWriteContext context);
    }

    private class DbWriteOperation<TResult>(
        Func<SqliteWriteContext, TResult> operation,
        string source,
        long enqueuedTimestamp) : ISyncDbWriteOperation
    {
        public readonly TaskCompletionSource<TResult> CompletionSource = new();

        public string Source { get; } = source;
        public long EnqueuedTimestamp { get; } = enqueuedTimestamp;
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
        long enqueuedTimestamp) : ISyncDbWriteOperation
    {
        public readonly TaskCompletionSource CompletionSource = new();

        public string Source { get; } = source;
        public long EnqueuedTimestamp { get; } = enqueuedTimestamp;
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
