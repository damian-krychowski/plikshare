using System.Collections.Concurrent;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.Core.Database.EventsDatabase;

/// <summary>
/// Serializes all hot-path writes to the events database. SQLite supports a single writer at a time,
/// so a dedicated single-thread loop is far more efficient than many connections contending for the
/// write lock — and it keeps this churn entirely off the main database's write queue.
/// </summary>
public class EventsDbWriteQueue(PlikShareEventsDb plikShareEventsDb) : IDisposable
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<EventsDbWriteQueue>();

    private readonly Lock _processingLock = new();
    private volatile bool _isProcessing;

    private readonly BlockingCollection<IDbWriteOperation> _requestsQueue = new();
    private readonly CancellationTokenSource _cancellationSource = new();

    private Task? _processTask;

    public Task Execute(
        Action<SqliteWriteContext> operationToEnqueue,
        CancellationToken cancellationToken)
    {
        var dbWriteRequest = new DbWriteOperation(
            operationToEnqueue);

        _requestsQueue.Add(
            item: dbWriteRequest,
            cancellationToken: cancellationToken);

        EnsureProcessingStarted();

        return dbWriteRequest.CompletionSource.Task;
    }

    public Task<TResult> Execute<TResult>(
        Func<SqliteWriteContext, TResult> operationToEnqueue,
        CancellationToken cancellationToken)
    {
        var dbWriteRequest = new DbWriteOperation<TResult>(
            operationToEnqueue);

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

            Logger.Debug("{QueueName} queue started", nameof(EventsDbWriteQueue));
        }
    }

    private void Process()
    {
        using var connection = plikShareEventsDb.OpenConnection();
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
                if (!_requestsQueue.TryTake(out var dbWriteOperation, TimeSpan.FromSeconds(1)))
                {
                    lock (_processingLock)
                    {
                        if (_requestsQueue.Count == 0)
                        {
                            _isProcessing = false;
                            Logger.Debug("{QueueName} queue stopped due to inactivity", nameof(EventsDbWriteQueue));
                            return;
                        }
                    }

                    continue;
                }

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
            catch (Exception ex)
            {
                Logger.Error(ex, "Error in events db write queue processing loop");
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
    }

    private interface ISyncDbWriteOperation : IDbWriteOperation
    {
        void Execute(SqliteWriteContext context);
    }

    private class DbWriteOperation<TResult>(Func<SqliteWriteContext, TResult> operation) : ISyncDbWriteOperation
    {
        public readonly TaskCompletionSource<TResult> CompletionSource = new();

        public void Execute(SqliteWriteContext context)
        {
            try
            {
                var result = operation(context);
                CompletionSource.SetResult(result);
            }
            catch (Exception e)
            {
                CompletionSource.SetException(e);
            }
        }
    }

    private class DbWriteOperation(Action<SqliteWriteContext> operation) : ISyncDbWriteOperation
    {
        public readonly TaskCompletionSource CompletionSource = new();

        public void Execute(SqliteWriteContext context)
        {
            try
            {
                operation(context);
                CompletionSource.SetResult();
            }
            catch (Exception e)
            {
                CompletionSource.SetException(e);
            }
        }
    }
}
