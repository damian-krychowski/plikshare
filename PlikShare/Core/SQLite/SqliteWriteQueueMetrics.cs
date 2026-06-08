using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace PlikShare.Core.SQLite;

public sealed class SqliteWriteQueueMetrics : IDisposable
{
    public const string MeterName = "PlikShare.SqliteWriteQueue";

    private readonly Meter _meter;
    private readonly Histogram<double> _queueWaitMs;
    private readonly Histogram<double> _executionMs;
    private readonly Histogram<double> _totalMs;
    private readonly Histogram<int> _enqueueDepth;
    private readonly Counter<long> _operations;
    private readonly ConcurrentDictionary<string, Func<int>> _depthProviders = new();

    public SqliteWriteQueueMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName);

        _queueWaitMs = _meter.CreateHistogram<double>(
            name: "plikshare.db_write.queue_wait",
            unit: "ms",
            description: "Time a write operation waited in the queue before execution started");

        _executionMs = _meter.CreateHistogram<double>(
            name: "plikshare.db_write.execution",
            unit: "ms",
            description: "Time a write operation spent executing on the writer thread");

        _totalMs = _meter.CreateHistogram<double>(
            name: "plikshare.db_write.total",
            unit: "ms",
            description: "End to end time of a write operation, queue wait plus execution");

        _enqueueDepth = _meter.CreateHistogram<int>(
            name: "plikshare.db_write.enqueue_depth",
            unit: "{operations}",
            description: "Number of operations already queued at the moment a new one was enqueued");

        _operations = _meter.CreateCounter<long>(
            name: "plikshare.db_write.operations",
            unit: "{operations}",
            description: "Count of completed write operations by source and outcome");

        _meter.CreateObservableGauge(
            name: "plikshare.db_write.queue_depth",
            observeValues: ObserveQueueDepth,
            unit: "{operations}",
            description: "Current number of operations waiting in the queue");
    }

    public void RegisterQueue(
        string queue,
        Func<int> depthProvider)
    {
        _depthProviders[queue] = depthProvider;
    }

    public void RecordEnqueue(
        string queue,
        int depthAtEnqueue)
    {
        _enqueueDepth.Record(
            depthAtEnqueue,
            new KeyValuePair<string, object?>("queue", queue));
    }

    public void RecordCompleted(
        string queue,
        string source,
        double queueWaitMs,
        double executionMs,
        bool success)
    {
        var queueTag = new KeyValuePair<string, object?>("queue", queue);
        var sourceTag = new KeyValuePair<string, object?>("source", source);

        _queueWaitMs.Record(queueWaitMs, queueTag, sourceTag);
        _executionMs.Record(executionMs, queueTag, sourceTag);
        _totalMs.Record(queueWaitMs + executionMs, queueTag, sourceTag);

        _operations.Add(
            1,
            queueTag,
            sourceTag,
            new KeyValuePair<string, object?>("outcome", success ? "success" : "error"));
    }

    private static readonly ConcurrentDictionary<(string?, string?), string> SourceCache = new();

    public static string BuildSource(
        string? callerFilePath,
        string? callerMember)
    {
        return SourceCache.GetOrAdd(
            (callerFilePath, callerMember),
            static key => BuildSourceCore(key.Item1, key.Item2));
    }

    private static string BuildSourceCore(
        string? callerFilePath,
        string? callerMember)
    {
        if (string.IsNullOrEmpty(callerFilePath))
            return string.IsNullOrEmpty(callerMember) ? "unknown" : callerMember;

        var separator = callerFilePath.LastIndexOfAny(['/', '\\']);
        var start = separator + 1;
        var dot = callerFilePath.LastIndexOf('.');
        var end = dot > start ? dot : callerFilePath.Length;

        var file = callerFilePath[start..end];

        return string.IsNullOrEmpty(callerMember) ? file : $"{file}.{callerMember}";
    }

    private IEnumerable<Measurement<int>> ObserveQueueDepth()
    {
        foreach (var (queue, provider) in _depthProviders)
        {
            yield return new Measurement<int>(
                provider(),
                new KeyValuePair<string, object?>("queue", queue));
        }
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
}
