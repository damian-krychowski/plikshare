using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace PlikShare.Core.SQLite;

public static class SqliteQueryMetrics
{
    public const string MeterName = "PlikShare.SqliteQueries";

    public const string KindRows = "rows";
    public const string KindOneRow = "one-row";
    public const string KindAggregate = "aggregate";
    public const string KindNonQuery = "non-query";

    private static readonly Meter Meter = new(MeterName);

    private static readonly Histogram<double> ExecutionMs = Meter.CreateHistogram<double>(
        name: "plikshare.db.query.execution",
        unit: "ms",
        description: "Execution time of a single SQL statement");

    private static readonly Histogram<int> Rows = Meter.CreateHistogram<int>(
        name: "plikshare.db.query.rows",
        unit: "{rows}",
        description: "Rows read or affected by a single SQL statement");

    private static readonly Counter<long> Calls = Meter.CreateCounter<long>(
        name: "plikshare.db.query.calls",
        unit: "{calls}",
        description: "Count of executed SQL statements by source, kind and outcome");

    public static void Record(
        string source,
        string kind,
        long startTimestamp,
        int rows,
        bool success)
    {
        var sourceTag = new KeyValuePair<string, object?>("source", source);
        var kindTag = new KeyValuePair<string, object?>("kind", kind);

        ExecutionMs.Record(
            Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds,
            sourceTag,
            kindTag);

        if (success)
        {
            Rows.Record(rows, sourceTag, kindTag);
        }

        Calls.Add(
            1,
            sourceTag,
            kindTag,
            new KeyValuePair<string, object?>("outcome", success ? "success" : "error"));
    }

    public static string ResolveSource(
        string? name,
        string? callerFilePath,
        string? callerMember)
    {
        return string.IsNullOrEmpty(name)
            ? SqliteWriteQueueMetrics.BuildSource(callerFilePath, callerMember)
            : name;
    }
}
