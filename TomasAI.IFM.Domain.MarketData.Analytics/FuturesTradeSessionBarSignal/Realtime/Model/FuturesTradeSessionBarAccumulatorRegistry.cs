using System.Collections.Concurrent;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Model;

/// <summary>
/// Owns a single-writer trade-session bar accumulator per market series and trading date.
/// </summary>
public sealed class FuturesTradeSessionBarAccumulatorRegistry(
    IMarketSessionCalendar calendar,
    IFuturesTradeSessionBarSeriesResolver seriesResolver,
    TimeProvider timeProvider)
{
    readonly ConcurrentDictionary<
        FuturesTradeSessionBarAccumulatorEntityId,
        FuturesTradeSessionBarAccumulator> accumulators = [];

    /// <summary>Resolves the same partition for routing and for trade processing.</summary>
    public FuturesTradeSessionBarAccumulatorEntityId Resolve(string contractId, DateOnly valueDate) =>
        new(valueDate, seriesResolver.Resolve(contractId).Format());

    /// <summary>Gets live series and recently ended sessions awaiting their final barrier.</summary>
    public IEnumerable<FuturesTradeSessionBarAccumulatorEntityId> ActivePartitions(DateTimeOffset barrierUtc)
    {
        var currentDate = calendar.GetValueDate(barrierUtc);
        foreach (var partition in accumulators.Keys)
        {
            if (partition.ValueDate == currentDate
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
            _ => new FuturesTradeSessionBarAccumulator(calendar, seriesResolver, timeProvider));
    }

    /// <summary>Gets the number of materialized accumulators.</summary>
    public int Count => accumulators.Count;
}
