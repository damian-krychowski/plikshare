namespace PlikShare.Core.SQLite;

public enum DbWritePriority
{
    Ui = 0,
    JobExtremelyHigh = 1,
    JobHigh = 2,
    JobNormal = 3,
    JobLow = 4,
    JobExtremelyLow = 5
}

public static class DbWritePriorityScope
{
    private static readonly AsyncLocal<DbWritePriority?> Current = new();

    public static DbWritePriority Effective => Current.Value ?? DbWritePriority.Ui;

    public static IDisposable BeginScope(DbWritePriority priority)
    {
        var previous = Current.Value;
        Current.Value = priority;
        return new Scope(previous);
    }

    private sealed class Scope(DbWritePriority? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            Current.Value = previous;
        }
    }
}
