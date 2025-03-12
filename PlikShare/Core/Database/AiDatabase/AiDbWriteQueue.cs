using System.Collections.Concurrent;
using Microsoft.Data.Sqlite;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.Core.Database.AiDatabase;

/// <summary>
/// this queue allows to serialize all hot path sqlite db writes. its important because SQLite support only one writer at the time
/// so even if someone will create separate connection to sqlite db below the surface they will be waiting for each other
/// which is much less efficient than create a single thread loop to run through all write operations. This way writes are not creating
/// separate connections and they can reuse the same SQLiteCommands which is quite a performance boost.
/// </summary>
/// <param name="plikShareAiDb"></param>
/// <param name="queue"></param>
/// <param name="clock"></param>
public class AiDbWriteQueue(PlikShareAiDb plikShareAiDb) : IDisposable
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<AiDbWriteQueue>();

    private readonly Lock _processingLock = new();
    private volatile bool _isProcessing;

    private readonly BlockingCollection<IDbWriteOperation> _requestsQueue = new();
    private readonly CancellationTokenSource _cancellationSource = new();

    private Task? _processTask;

    public Task Execute(
        Action<Context> operationToEnqueue,
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

    public Task Execute(
        Func<Context, CancellationToken, ValueTask> operationToEnqueue,
        CancellationToken cancellationToken)
    {
        var dbWriteRequest = new AsyncDbWriteOperation(
            operationToEnqueue);

        _requestsQueue.Add(
            item: dbWriteRequest,
            cancellationToken: cancellationToken);

        EnsureProcessingStarted();

        return dbWriteRequest.CompletionSource.Task;
    }

    public Task<TResult> Execute<TResult>(
        Func<Context, TResult> operationToEnqueue,
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

    public Task<TResult> Execute<TResult>(
        Func<Context, CancellationToken, ValueTask<TResult>> operationToEnqueue,
        CancellationToken cancellationToken)
    {
        var dbWriteRequest = new AsyncDbWriteOperation<TResult>(
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

            Logger.Debug("{QueueName} queue started", nameof(AiDbWriteQueue));
        }
    }

    private async Task Process()
    {
        using var connection = plikShareAiDb.OpenConnection();
        using var commandsPool = connection.CreateLazyCommandsPool();

        var context = new Context
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
                            Logger.Debug("{QueueName} queue stopped due to inactivity", nameof(AiDbWriteQueue));
                            return;
                        }
                    }

                    continue;
                }

                switch (dbWriteOperation)
                {
                    case IAsyncDbWriteOperation asyncDbWriteOperation:
                        await asyncDbWriteOperation.Execute(
                            context,
                            _cancellationSource.Token);
                        break;

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
    }

    private interface ISyncDbWriteOperation : IDbWriteOperation
    {
        void Execute(Context context);
    }

    private interface IAsyncDbWriteOperation : IDbWriteOperation
    {
        ValueTask Execute(Context context, CancellationToken cancellationToken);
    }

    private class AsyncDbWriteOperation<TResult>(Func<Context, CancellationToken, ValueTask<TResult>> operation) : IAsyncDbWriteOperation
    {
        public readonly TaskCompletionSource<TResult> CompletionSource = new();

        public async ValueTask Execute(Context context, CancellationToken cancellationToken)
        {
            try
            {
                var result = await operation(context, cancellationToken);
                CompletionSource.SetResult(result);
            }
            catch (Exception e)
            {
                CompletionSource.SetException(e);
            }
        }
    }

    private class DbWriteOperation<TResult>(Func<Context, TResult> operation) : ISyncDbWriteOperation
    {
        public readonly TaskCompletionSource<TResult> CompletionSource = new();

        public void Execute(Context context)
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

    private class AsyncDbWriteOperation(Func<Context, CancellationToken, ValueTask> operation) : IAsyncDbWriteOperation
    {
        public readonly TaskCompletionSource CompletionSource = new();

        public async ValueTask Execute(Context context, CancellationToken cancellationToken)
        {
            try
            {
                await operation(context, cancellationToken);
                CompletionSource.SetResult();
            }
            catch (Exception e)
            {
                CompletionSource.SetException(e);
            }
        }
    }

    private class DbWriteOperation(Action<Context> operation) : ISyncDbWriteOperation
    {
        public readonly TaskCompletionSource CompletionSource = new();

        public void Execute(Context context)
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

    public class Context
    {
        public required SqliteConnection Connection { get; init; }
        public required LazySqLiteCommandsPool CommandsPool { get; init; }
    }
}

public static class AiDbWriteQueueContextExtensions
{
    public static void DeferForeignKeys(
        this AiDbWriteQueue.Context dbWriteContext,
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