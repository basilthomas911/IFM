namespace TomasAI.IFM.Application.Storage.EventSourceDb;

/// <summary>Server-enforced limits for one SQL statement; caller cancellation remains authoritative.</summary>
public sealed record PostgresQueryTimeout(TimeSpan StatementTimeout, TimeSpan? LockTimeout = null)
{
    /// <summary>Default SQL and lock wait budget when a query does not specify its own limits.</summary>
    public static PostgresQueryTimeout Default { get; } = new(TimeSpan.FromMinutes(2));

    /// <summary>Provider fallback for commands outside the enlisted query executor.</summary>
    public int CommandTimeoutSeconds => checked((int)Math.Ceiling(StatementTimeout.TotalSeconds));

    internal PostgresQueryTimeout Validate()
    {
        var locks = LockTimeout ?? StatementTimeout;
        if (StatementTimeout <= TimeSpan.Zero || StatementTimeout.TotalMilliseconds > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(StatementTimeout), "SQL timeout must be positive and fit PostgreSQL milliseconds.");
        if (locks <= TimeSpan.Zero || locks.TotalMilliseconds > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(LockTimeout), "Lock timeout must be positive and fit PostgreSQL milliseconds.");
        return this;
    }
}
