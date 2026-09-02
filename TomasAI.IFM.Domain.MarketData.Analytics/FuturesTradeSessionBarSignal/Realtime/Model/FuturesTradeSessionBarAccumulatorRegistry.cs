using System.Collections.Concurrent;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Model;

/// <summary>
/// Owns one ephemeral, single-writer trade-session bar accumulator per futures value date.
/// The actor mailbox provides serialization within a date; this registry isolates independent dates.
/// </summary>
public sealed class FuturesTradeSessionBarAccumulatorRegistry(
    IMarketSessionCalendar calendar,
    IFuturesTradeSessionBarSeriesResolver seriesResolver,
    TimeProvider timeProvider)
{
    readonly ConcurrentDictionary<
        FuturesTradeSessionBarAccumulatorEntityId,
        FuturesTradeSessionBarAccumulator> accumulators = [];

    /// <summary>Gets the accumulator owned by the supplied value-date identity.</summary>
    public FuturesTradeSessionBarAccumulator Get(
        FuturesTradeSessionBarAccumulatorEntityId entityId)
    {
        var errors = new FuturesTradeSessionBarAccumulatorEntityIdValidationRules().Execute(entityId);
        if (errors.Length != 0)
            throw new ArgumentException(
                string.Join("; ", errors.Select(value => value.ErrorMessage)),
                nameof(entityId));
        return accumulators.GetOrAdd(
            entityId,
            _ => new FuturesTradeSessionBarAccumulator(calendar, seriesResolver, timeProvider));
    }

    /// <summary>Gets the number of value-date accumulators currently materialized.</summary>
    public int Count => accumulators.Count;
}
