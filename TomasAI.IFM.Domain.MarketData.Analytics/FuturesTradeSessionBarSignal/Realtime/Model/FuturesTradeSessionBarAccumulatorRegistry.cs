using System.Collections.Concurrent;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Model;

/// <summary>
/// Owns a single-writer trade-session bar accumulator per market series and trading date.
/// </summary>
public sealed class FuturesTradeSessionBarAccumulatorRegistry(
    IMarketSessionCalendar calendar,
    IFuturesTradeSessionBarSeriesResolver seriesResolver,
    TimeProvider timeProvider,
    FuturesTradeSessionBarCheckpointStore? checkpoints = null)
{
    readonly ConcurrentDictionary<
        FuturesTradeSessionBarAccumulatorEntityId,
        FuturesTradeSessionBarAccumulator> accumulators = [];

    readonly ConcurrentDictionary<FuturesTradeSessionBarAccumulatorEntityId, SemaphoreSlim> gates = [];
    readonly Lock restoreGate = new();
    bool restored;

    /// <summary>Restores all committed partitions before live routes are registered.</summary>
    public void Restore()
    {
        if (checkpoints is null) return;
        lock (restoreGate)
        {
            // Actor generation replacement reuses this registry. Do not reopen writers owned by an in-flight handler.
            if (restored) return;
            foreach (var id in checkpoints.Partitions()) _ = Get(id);
            restored = true;
        }
    }

    /// <summary>Resolves the same partition for routing and for trade processing.</summary>
    public FuturesTradeSessionBarAccumulatorEntityId Resolve(string contractId, DateOnly valueDate) =>
        new(valueDate, seriesResolver.Resolve(contractId).Format());

    /// <summary>Gets live series and recently ended sessions awaiting their final barrier.</summary>
    public IEnumerable<FuturesTradeSessionBarAccumulatorEntityId> ActivePartitions(DateTimeOffset barrierUtc)
    {
        var currentDate = calendar.GetValueDate(barrierUtc);
        foreach (var (partition, accumulator) in accumulators)
        {
            if (accumulator.HasRecoveryWork
                || partition.ValueDate == currentDate
                || (partition.ValueDate < currentDate
                    && calendar.GetSession(partition.ValueDate).EndUtc > barrierUtc.AddMinutes(-1)))
                yield return partition;
        }
    }

    /// <summary>Gets the accumulator owned by the supplied partition.</summary>
    public FuturesTradeSessionBarAccumulator Get(
        FuturesTradeSessionBarAccumulatorEntityId entityId)
    {
        if (accumulators.TryGetValue(entityId, out var active)) return active;
        var errors = new FuturesTradeSessionBarAccumulatorEntityIdValidationRules().Execute(entityId);
        if (errors.Length != 0)
            throw new ArgumentException(
                string.Join("; ", errors.Select(value => value.ErrorMessage)),
                nameof(entityId));
        return accumulators.GetOrAdd(
            entityId,
            id =>
            {
                var accumulator = new FuturesTradeSessionBarAccumulator(calendar, seriesResolver, timeProvider);
                checkpoints?.Restore(id, accumulator);
                return accumulator;
            });
    }

    /// <summary>Commits input and pending bars before publication; failed/ambiguous handoffs remain replayable.</summary>
    public async ValueTask ProcessAsync(
        FuturesTradeSessionBarAccumulatorEntityId id,
        Func<FuturesTradeSessionBarAccumulator, IReadOnlyList<FuturesTradeSessionBarReadModel>> apply,
        Func<FuturesTradeSessionBarReadModel, ValueTask> publish)
    {
        var gate = gates.GetOrAdd(id, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var accumulator = Get(id);
            // Older pending bars must commit before a new input can overtake them.
            await FlushAsync(accumulator).ConfigureAwait(false);
            using (RealtimeActorGeneration.EnterMutation())
            {
                accumulator.PendingPublications.AddRange(apply(accumulator));
                checkpoints?.Save(id, accumulator);
            }
            await FlushAsync(accumulator).ConfigureAwait(false);
            if (!accumulator.HasRecoveryWork) checkpoints?.Release(id);
        }
        catch
        {
            // A failed disk write may have committed or not. Reload authoritative state next time.
            // In-memory test registries retain ownership of their pending bars on transport failure.
            if (checkpoints is not null)
            {
                accumulators.TryRemove(id, out _);
                _ = Get(id);
            }
            throw;
        }
        finally { gate.Release(); }

        async ValueTask FlushAsync(FuturesTradeSessionBarAccumulator accumulator)
        {
            while (accumulator.PendingPublications.Count != 0)
            {
                var bar = accumulator.PendingPublications[0];
                await publish(bar).ConfigureAwait(false);
                using (RealtimeActorGeneration.EnterMutation())
                {
                    accumulator.PendingPublications.RemoveAt(0);
                    checkpoints?.Save(id, accumulator);
                }
            }
        }
    }

    /// <summary>Gets the number of materialized accumulators.</summary>
    public int Count => accumulators.Count;
}
