using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Service.OptionPricer;

public class LossProbability(List<double> putSpreadValues, List<double> callSpreadValues, double maxLoss)
{
    readonly List<double> _putSpreadValues = putSpreadValues;
    readonly List<double> _callSpreadValues = callSpreadValues;
    readonly double _maxLoss = maxLoss;

    public LossProbabilityDataModel ToViewModel(List<double> putSpreadPnl, List<double> callSpreadPnl)
    {
        var expectedPnl = new List<double>();
        for (var index = 0; index < putSpreadPnl.Count; index++)
        {
            var putPnl = putSpreadPnl[index];
            var callPnl = callSpreadPnl[index];
            expectedPnl.Add(putPnl + callPnl);
        }
        return new LossProbabilityDataModel ( GetLossProbability(expectedPnl), 0, 0);
    }

    public static LossProbabilityDataModel Empty => new  (
        Value: 0,
        Threshold: 0,
        ThresholdCount: 0
    );

    public IEnumerable<double> GetExpectedPnlValues(OptionType optionType, int quantity, double multiplier, double netSpread)
       => optionType == OptionType.Put
           ? _putSpreadValues.Select(spreadValue => (spreadValue - netSpread) * quantity * multiplier)
           : _callSpreadValues.Select(spreadValue => (netSpread - spreadValue) * quantity * multiplier);

    double GetLossProbability(ICollection<double> expectedPnlValues)
    {
        // check if min value exceeds loss limit...
        var lossValue = expectedPnlValues.Min();
        if (lossValue <= _maxLoss) 
            return 1.0;

        // get median...
        var median = expectedPnlValues.OrderBy(e => e).ToArray()[(int)(expectedPnlValues.Count / 2)];

        // get the absolute deviations from the median...
        var absDevsFromMedian = expectedPnlValues.Select(x => Math.Abs(x - median)).ToList();

        // get the median of the absolute deviation values...
        var medianAbsDev = absDevsFromMedian.OrderBy(e => e).ToArray()[(int)(absDevsFromMedian.Count / 2)];
        return Math.Abs((median - (3.5 * medianAbsDev)) / _maxLoss);
    }
}
