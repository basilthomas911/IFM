using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;

/// <summary>Chooses the earliest permitted future before economic ranking, using exact last-trade evidence.</summary>
public static class EsFuturesOrderComposer
{
    public static IEnumerable<(CompositionInstrumentSnapshot Instrument, int Sign)[]> Enumerate(
        MarketCompositionSnapshot snapshot, CompositionParameters p, string side)
    {
        var future = snapshot.Instruments.Where(x => x.Instrument.FutureDefinition is { } f &&
                f.LastTradingUtc > snapshot.EvaluatedAtUtc.AddHours((double)p.FuturesRollHours))
            .OrderBy(x => x.Instrument.FutureDefinition!.LastTradingUtc)
            .ThenBy(x => x.Instrument.ContractId, StringComparer.Ordinal).FirstOrDefault();
        if (future is not null) yield return [(future, side == "Long" ? 1 : -1)];
    }
}

/// <summary>Enumerates actual same-expiry vertical strikes; no synthetic contract or ratio is introduced.</summary>
public static class EsVerticalSpreadOrderComposer
{
    public static IEnumerable<(CompositionInstrumentSnapshot Instrument, int Sign)[]> Enumerate(
        CompositionInstrumentSnapshot[] options, bool call, string side, CompositionVariantRules rules, CancellationToken token = default)
    {
        var chain = options.Where(x => x.Instrument.IsCall == call).OrderBy(x => x.Instrument.Strike)
            .ThenBy(x => x.Instrument.ContractId, StringComparer.Ordinal).ToArray();
        int lower = call ? (side == "Long" ? 1 : -1) : (side == "Long" ? -1 : 1);
        for (int i = 0; i < chain.Length; i++)
        for (int j = i + 1; j < chain.Length; j++)
        {
            token.ThrowIfCancellationRequested();
            if (rules.AllowedWidths.Contains(chain[j].Instrument.Strike!.Value - chain[i].Instrument.Strike!.Value))
                yield return [(chain[i], lower), (chain[j], -lower)];
        }
    }
}

/// <summary>Enumerates both long and short condors, retaining unequal wings when explicitly permitted.</summary>
public static class EsIronCondorOrderComposer
{
    public static IEnumerable<(CompositionInstrumentSnapshot Instrument, int Sign)[]> Enumerate(
        CompositionInstrumentSnapshot[] options, string side, CompositionVariantRules rules, CancellationToken token = default)
    {
        var puts = EsVerticalSpreadOrderComposer.Enumerate(options, false, "Long", rules, token).ToArray();
        var calls = EsVerticalSpreadOrderComposer.Enumerate(options, true, "Short", rules, token).ToArray();
        int direction = side == "Short" ? -1 : 1;
        foreach (var put in puts)
        foreach (var call in calls)
        {
            token.ThrowIfCancellationRequested();
            if (put[1].Instrument.Instrument.Strike >= call[0].Instrument.Instrument.Strike) continue;
            var wp = put[1].Instrument.Instrument.Strike - put[0].Instrument.Instrument.Strike;
            var wc = call[1].Instrument.Instrument.Strike - call[0].Instrument.Instrument.Strike;
            if (rules.RequireSymmetricWings && wp != wc) continue;
            yield return [(put[0].Instrument, -direction), (put[1].Instrument, direction),
                (call[0].Instrument, direction), (call[1].Instrument, -direction)];
        }
    }
}
