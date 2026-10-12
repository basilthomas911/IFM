namespace TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Realtime.Projector;

/// <summary>One opt-in, bounded observation of generation-correlated successful tick storage writes.</summary>
/// <remarks>The evidence is recorded only after the database command completes; it does not modify tick rows
/// or itself authorize downstream admission.</remarks>
public sealed class TickStorageGenerationEvidence(TimeProvider? time = null)
{
    readonly System.Threading.Lock gate = new();
    readonly TimeProvider clock = time ?? TimeProvider.System;
    TickStorageGenerationTarget? target;
    long completedWrites;
    DateTimeOffset? lastCompletedUtc;

    /// <summary>Arms one exact dataset and generation, discarding earlier evidence.</summary>
    public void Arm(TickStorageGenerationTarget value)
    {
        ArgumentNullException.ThrowIfNull(value);
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
            if (target is not { } expected || !string.Equals(expected.Dataset,dataset,StringComparison.Ordinal)
                || expected.GenerationId != generationId)
                return;
            completedWrites++;
            lastCompletedUtc = clock.GetUtcNow();
        }
    }
}
