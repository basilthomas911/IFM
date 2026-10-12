using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
/// <summary>Chooses the earliest permitted future before economic ranking, using exact last-trade evidence.</summary>
public static class EsFuturesOrderComposer
{
    public static IEnumerable<(CompositionInstrumentSnapshot Instrument, int Sign)[]> Enumerate(MarketCompositionSnapshot snapshot, CompositionParameters p, string side)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return EnumerateCore(snapshot, p, side);
    }

    static IEnumerable<(CompositionInstrumentSnapshot Instrument, int Sign)[]> EnumerateCore(MarketCompositionSnapshot snapshot, CompositionParameters p, string side)
    {
        var future = snapshot.Instruments.Where(x => x.Instrument.FutureDefinition is { } f && f.LastTradingUtc > snapshot.EvaluatedAtUtc.AddHours((double)p.FuturesRollHours)).OrderBy(x => x.Instrument.FutureDefinition!.LastTradingUtc).ThenBy(x => x.Instrument.ContractId, StringComparer.Ordinal).FirstOrDefault();
        if (future is not null)
            yield return[(future, string.Equals(side, "Long", StringComparison.Ordinal) ? 1 : -1)];
    }
}
