using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
/// <summary>Enumerates both long and short condors, retaining unequal wings when explicitly permitted.</summary>
public static class EsIronCondorOrderComposer
{
    public static IEnumerable<(CompositionInstrumentSnapshot Instrument, int Sign)[]> Enumerate(CompositionInstrumentSnapshot[] options, string side, CompositionVariantRules rules, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return EnumerateCore(options, side, rules, token);
    }

    static IEnumerable<(CompositionInstrumentSnapshot Instrument, int Sign)[]> EnumerateCore(CompositionInstrumentSnapshot[] options, string side, CompositionVariantRules rules, CancellationToken token = default)
    {
        var puts = EsVerticalSpreadOrderComposer.Enumerate(options, false, "Long", rules, token).ToArray();
        var calls = EsVerticalSpreadOrderComposer.Enumerate(options, true, "Short", rules, token).ToArray();
        int direction = string.Equals(side, "Short", StringComparison.Ordinal) ? -1 : 1;
        foreach (var put in puts)
            foreach (var call in calls)
            {
                token.ThrowIfCancellationRequested();
                if (put[1].Instrument.Instrument.Strike >= call[0].Instrument.Instrument.Strike)
                    continue;
                var wp = put[1].Instrument.Instrument.Strike - put[0].Instrument.Instrument.Strike;
                var wc = call[1].Instrument.Instrument.Strike - call[0].Instrument.Instrument.Strike;
                if (rules.RequireSymmetricWings && wp != wc)
                    continue;
                yield return[(put[0].Instrument, -direction), (put[1].Instrument, direction), (call[0].Instrument, direction), (call[1].Instrument, -direction)];
            }
    }
}
