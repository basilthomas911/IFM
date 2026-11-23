namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Runtime;

/// <summary>A producer-owned, already validated input generation. Publication never changes observation timestamps.</summary>
public sealed class PreparedDecisionInput<T> where T : class
{
    sealed record Generation(T? Value, long Revision, DateTime ValidUntilUtc);
    Generation? current;

    /// <summary>Publishes a complete generation after the owning producer has validated it.</summary>
    public void Publish(T value, long revision, DateTime validUntilUtc)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (revision <= 0 || validUntilUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("A positive revision and UTC validity boundary are required.");
        var next = new Generation(value, revision, validUntilUtc);
        while (true)
        {
            var previous = Volatile.Read(ref current);
            if (previous is not null && previous.Revision >= revision) return;
            if (ReferenceEquals(Interlocked.CompareExchange(ref current, next, previous), previous)) return;
        }
    }

    /// <summary>Acquires a ready reference immediately. This operation cannot load or validate data.</summary>
    public bool TryAcquire(DateTime nowUtc, out T? value, out long revision)
    {
        var generation = Volatile.Read(ref current);
        var ready = generation?.Value is not null && nowUtc < generation.ValidUntilUtc;
        value = ready ? generation!.Value : null;
        revision = generation?.Revision ?? 0;
        return ready;
    }

    /// <summary>Invalidates only the observed generation, without discarding a newer publication.</summary>
    public void Invalidate(long revision)
    {
        var generation = Volatile.Read(ref current);
        if (generation?.Revision == revision)
            Interlocked.CompareExchange(ref current, new(null, revision, DateTime.MinValue), generation);
    }
}
