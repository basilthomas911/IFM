using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
/// <summary>Enumerates actual same-expiry vertical strikes; no synthetic contract or ratio is introduced.</summary>
public static class EsVerticalSpreadOrderComposer
{
    public static IEnumerable<(CompositionInstrumentSnapshot Instrument, int Sign)[]> Enumerate(CompositionInstrumentSnapshot[] options, bool call, string side, CompositionVariantRules rules, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return EnumerateCore(options, call, side, rules, token);
    }

    static IEnumerable<(CompositionInstrumentSnapshot Instrument, int Sign)[]> EnumerateCore(CompositionInstrumentSnapshot[] options, bool call, string side, CompositionVariantRules rules, CancellationToken token = default)
    {
        var chain = options.Where(x => x.Instrument.IsCall == call).OrderBy(x => x.Instrument.Strike).ThenBy(x => x.Instrument.ContractId, StringComparer.Ordinal).ToArray();
        int lower = ((call) switch
        {
            true => (string.Equals(side, "Long", StringComparison.Ordinal) ? 1 : -1),
            _ => (string.Equals(side, "Long", StringComparison.Ordinal) ? -1 : 1)});
        for (int i = 0; i < chain.Length; i++)
            for (int j = i + 1; j < chain.Length; j++)
            {
                token.ThrowIfCancellationRequested();
                if (rules.AllowedWidths.Contains(chain[j].Instrument.Strike!.Value - chain[i].Instrument.Strike!.Value))
                    yield return[(chain[i], lower), (chain[j], -lower)];
            }
    }
}
