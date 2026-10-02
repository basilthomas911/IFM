namespace TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Realtime.Projector;

/// <summary>One opt-in, bounded observation of generation-correlated successful tick storage writes.</summary>
/// <remarks>The evidence is recorded only after the database command completes; it does not modify tick rows
/// or itself authorize downstream admission.</remarks>
public sealed class TickStorageGenerationEvidence(TimeProvider? time = null)
{
    readonly object gate = new();
    readonly TimeProvider clock = time ?? TimeProvider.System;
    TickStorageGenerationTarget? target;
    long completedWrites;
    DateTimeOffset? lastCompletedUtc;

    /// <summary>Arms one exact dataset and generation, discarding earlier evidence.</summary>
    public void Arm(TickStorageGenerationTarget value)
    {
        if (string.IsNullOrWhiteSpace(value.Dataset) || value.Dataset.Length > 64
            || value.GenerationId == Guid.Empty)
            throw new ArgumentException("A complete tick-storage generation is required.", nameof(value));
        lock (gate)
        {
            target = value;
            completedWrites = 0;
            lastCompletedUtc = null;
        }
    }

    /// <summary>Disarms observation and discards the prior generation's evidence.</summary>
    public void Disarm()
    {
        lock (gate)
        {
            target = null;
            completedWrites = 0;
            lastCompletedUtc = null;
        }
    }

    /// <summary>Captures only aggregate evidence; no individual tick is retained.</summary>
    public TickStorageGenerationSnapshot Capture()
    {
        lock (gate) return new(target, completedWrites, lastCompletedUtc);
    }

    internal void Record(string dataset, Guid generationId)
    {
        lock (gate)
        {
            if (target is not { } expected || expected.Dataset != dataset
                || expected.GenerationId != generationId)
                return;
            completedWrites++;
            lastCompletedUtc = clock.GetUtcNow();
        }
    }
}

/// <summary>Exact dataset generation observed at the tick storage boundary.</summary>
public sealed record TickStorageGenerationTarget(string Dataset, Guid GenerationId);

/// <summary>Aggregate completed-write evidence, not an admission decision.</summary>
public sealed record TickStorageGenerationSnapshot(TickStorageGenerationTarget? Target,
    long CompletedWrites, DateTimeOffset? LastCompletedUtc);
