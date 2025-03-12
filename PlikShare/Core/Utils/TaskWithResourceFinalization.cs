namespace PlikShare.Core.Utils;


/// <summary>
/// The goal of this class is to make sure that a resource finalization will happen only once
/// and that it will happen for sure for the scenarios where task can have a continuation, but
/// that the continuation is not required. So in normal case we will finalize the executed task and call @finally method
/// But in case of continuation, original finalization is prevented with the flag and the finalization is reused and performed
/// when the continuation is finished.
/// </summary>
public class TaskWithResourceFinalization
{
    private Task? _task;
    private Func<ValueTask>? _finallyFunc;

    private bool _shouldExecuteOriginalTaskFinallyAction = true;
    private bool _wasFinished = false;

    public Task Execute(Func<Task> @try, Func<ValueTask> @finally)
    {
        _finallyFunc = @finally;
        _task = ExecuteTask(@try, @finally);

        return _task;
    }

    private async Task ExecuteTask(Func<Task> @try, Func<ValueTask> @finally)
    {
        try
        {
            await @try();
        }
        finally
        {
            if (_shouldExecuteOriginalTaskFinallyAction)
            {
                await @finally();
            }

            _wasFinished = true;
        }
    }

    public async Task ContinueWith(Func<Task> continuationFunction, CancellationToken cancellationToken)
    {
        if (_task is null || _finallyFunc is null)
            throw new InvalidOperationException(
                "There is nothing to continue as no task is running at the moment");

        if (_wasFinished)
            throw new InvalidOperationException(
                "Original task was already finished and resources have been finalized before continuation started.");

        try
        {
            _shouldExecuteOriginalTaskFinallyAction = false;

            await _task
                .ContinueWith(
                    continuationFunction: _ => continuationFunction(),
                    cancellationToken: cancellationToken)
                .Unwrap();
        }
        finally
        {
            await _finallyFunc();
        }
    }
}