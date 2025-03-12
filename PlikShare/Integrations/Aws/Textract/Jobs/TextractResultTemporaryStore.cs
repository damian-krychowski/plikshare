using System.Collections.Concurrent;
using Amazon.Textract.Model;
using PlikShare.Core.Clock;

namespace PlikShare.Integrations.Aws.Textract.Jobs;

public class TextractResultTemporaryStore(IClock clock) : IDisposable
{
    private readonly ConcurrentDictionary<Guid, Data> _textractResults = new();
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(30);
    private Timer? _cleanupTimer;
    private bool _disposed;
    private readonly Lock _timerLock = new();

    public Id Store(GetDocumentAnalysisResponse documentAnalysisResponse)
    {
        var id = new Id(
            Value: Guid.NewGuid());

        var data = new Data
        {
            Value = documentAnalysisResponse,
            ExpirationDate = clock.UtcNow.AddMinutes(15)
        };

        _textractResults.AddOrUpdate(
            key: id.Value,
            addValueFactory: _ => data,
            updateValueFactory: (_, _) => data);

        // Start cleanup timer if it's the first item
        EnsureCleanupTimerStarted();

        return id;
    }

    public GetDocumentAnalysisResponse? TryGet(Guid id)
    {
        return _textractResults.TryGetValue(id, out var data)
            ? data.Value
            : null;
    }

    public void Remove(Id id)
    {
        _textractResults.TryRemove(id.Value, out _);

        // Stop cleanup timer if dictionary is empty
        if (_textractResults.IsEmpty)
        {
            StopCleanupTimer();
        }
    }

    private void EnsureCleanupTimerStarted()
    {
        lock (_timerLock)
        {
            if (_cleanupTimer == null && !_disposed)
            {
                _cleanupTimer = new Timer(
                    CleanupExpiredEntries,
                    null,
                    _cleanupInterval,
                    _cleanupInterval);
            }
        }
    }

    private void StopCleanupTimer()
    {
        lock (_timerLock)
        {
            if (_cleanupTimer != null)
            {
                _cleanupTimer.Dispose();
                _cleanupTimer = null;
            }
        }
    }

    private void CleanupExpiredEntries(object? state)
    {
        var expiredKeys = _textractResults
            .Where(kvp => kvp.Value.ExpirationDate < clock.UtcNow)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _textractResults.TryRemove(key, out _);
        }

        // Stop timer if no items left
        if (_textractResults.IsEmpty)
        {
            StopCleanupTimer();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            StopCleanupTimer();
        }

        _disposed = true;
    }

    private class Data
    {
        public required GetDocumentAnalysisResponse Value { get; init; }
        public required DateTimeOffset ExpirationDate { get; init; }
    }

    public readonly record struct Id(Guid Value);
}