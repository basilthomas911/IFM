using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using System.Buffers;

namespace TomasAI.IFM.Framework.OptionPricer.Black76;

/// <summary>
/// Computes the probability of loss for an Iron Condor options strategy using a Median Absolute Deviation (MAD)
/// based statistical model.
/// </summary>
/// <remarks>
/// The loss probability is estimated by combining simulated put and call spread P&amp;L distributions and comparing
/// the resulting expected P&amp;L against a maximum loss threshold. When the worst-case expected P&amp;L exceeds
/// the threshold, the probability is 1.0 (certain loss). Otherwise, a MAD-based z-score approach is used to
/// estimate how far the median P&amp;L sits from the loss boundary, expressed as a ratio of the max loss.
/// </remarks>
/// <param name="putSpreadValues">Simulated spread values from the put credit spread Monte Carlo pricing.</param>
/// <param name="callSpreadValues">Simulated spread values from the call credit spread Monte Carlo pricing.</param>
/// <param name="maxLoss">The maximum allowable loss threshold (negative value) for the trade position.</param>
public class LossProbability(IReadOnlyList<double> putSpreadValues, IReadOnlyList<double> callSpreadValues, double maxLoss)
{
    readonly IReadOnlyList<double> _putSpreadValues = putSpreadValues;
    readonly IReadOnlyList<double> _callSpreadValues = callSpreadValues;
    readonly double _maxLoss = maxLoss;

    /// <summary>
    /// Combines the put and call spread P&amp;L values element-wise and computes the overall loss probability.
    /// </summary>
    /// <param name="putSpreadPnl">The expected P&amp;L values for the put credit spread, one per simulation path.</param>
    /// <param name="callSpreadPnl">The expected P&amp;L values for the call credit spread, one per simulation path. Must have the same
    /// count as <paramref name="putSpreadPnl"/>.</param>
    /// <returns>
    /// A <see cref="LossProbabilityDataModel"/> containing the computed loss probability value. The
    /// <c>Threshold</c> and <c>ThresholdCount</c> fields are set to zero.
    /// </returns>
    public LossProbabilityDataModel ToViewModel(List<double> putSpreadPnl, List<double> callSpreadPnl)
    {
        var expectedPnl = new List<double>();
        for (var index = 0; index < putSpreadPnl.Count; index++)
        {
            var putPnl = putSpreadPnl[index];
            var callPnl = callSpreadPnl[index];
            expectedPnl.Add(putPnl + callPnl);
        }
        return new LossProbabilityDataModel(GetLossProbability(expectedPnl), 0, 0);
    }

    /// <summary>
    /// Calculates combined put/call P&amp;L and its MAD-based loss probability in one pass,
    /// avoiding the two projected lists and a third combined list used by the compatibility API.
    /// </summary>
    public LossProbabilityDataModel Calculate(
        int quantity,
        double multiplier,
        double putNetSpread,
        double callNetSpread)
    {
        if (_putSpreadValues.Count != _callSpreadValues.Count)
            throw new InvalidOperationException("Put and call spread simulation counts must match.");
        if (_putSpreadValues.Count == 0)
            return Empty;

        var count = _putSpreadValues.Count;
        var values = ArrayPool<double>.Shared.Rent(count);
        try
        {
            var scale = quantity * multiplier;
            var minimum = double.PositiveInfinity;
            for (var index = 0; index < count; index++)
            {
                var putPnl = (_putSpreadValues[index] - putNetSpread) * scale;
                var callPnl = (callNetSpread - _callSpreadValues[index]) * scale;
                var combined = putPnl + callPnl;
                values[index] = combined;
                minimum = Math.Min(minimum, combined);
            }

            if (minimum <= _maxLoss)
                return new LossProbabilityDataModel(1.0, 0, 0);

            Array.Sort(values, 0, count);
            var median = values[count / 2];
            for (var index = 0; index < count; index++)
                values[index] = Math.Abs(values[index] - median);

            Array.Sort(values, 0, count);
            var medianAbsoluteDeviation = values[count / 2];
            return new LossProbabilityDataModel(
                Math.Abs((median - (3.5 * medianAbsoluteDeviation)) / _maxLoss),
                0,
                0);
        }
        finally
        {
            ArrayPool<double>.Shared.Return(values);
        }
    }

    /// <summary>
    /// Gets an empty <see cref="LossProbabilityDataModel"/> with all fields set to zero, representing no loss risk.
    /// </summary>
    public static LossProbabilityDataModel Empty => new(
        Value: 0,
        Threshold: 0,
        ThresholdCount: 0
    );

    /// <summary>
    /// Projects the simulated spread values into expected P&amp;L values for a given option type, adjusting for
    /// position quantity, contract multiplier, and net spread premium.
    /// </summary>
    /// <remarks>
    /// For put spreads, each P&amp;L is calculated as <c>(spreadValue - netSpread) × quantity × multiplier</c>.
    /// For call spreads, each P&amp;L is calculated as <c>(netSpread - spreadValue) × quantity × multiplier</c>.
    /// The sign convention reflects that put credit spreads profit when spreads narrow and call credit spreads
    /// profit when spreads widen relative to the premium received.
    /// </remarks>
    /// <param name="optionType">The option type (<see cref="OptionType.Put"/> or <see cref="OptionType.Call"/>) determining which
    /// set of simulated spread values and P&amp;L formula to use.</param>
    /// <param name="quantity">The number of contracts in the position.</param>
    /// <param name="multiplier">The contract multiplier (e.g., 50 for E-mini S&amp;P 500 options).</param>
    /// <param name="netSpread">The net credit or debit spread premium received or paid when entering the position.</param>
    /// <returns>An enumerable of expected P&amp;L values, one per simulation path.</returns>
    public IEnumerable<double> GetExpectedPnlValues(OptionType optionType, int quantity, double multiplier, double netSpread)
       => optionType == OptionType.Put
           ? _putSpreadValues.Select(spreadValue => (spreadValue - netSpread) * quantity * multiplier)
           : _callSpreadValues.Select(spreadValue => (netSpread - spreadValue) * quantity * multiplier);

    /// <summary>
    /// Computes the loss probability using a Median Absolute Deviation (MAD) based approach.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The algorithm proceeds as follows:
    /// </para>
    /// <list type="number">
    /// <item><description>If the minimum expected P&amp;L is at or below the max loss threshold, the probability
    /// is 1.0 (certain loss).</description></item>
    /// <item><description>The median of the expected P&amp;L distribution is computed.</description></item>
    /// <item><description>The Median Absolute Deviation (MAD) is computed as the median of the absolute deviations
    /// from the median.</description></item>
    /// <item><description>A modified z-score boundary is calculated as <c>median − 3.5 × MAD</c>, representing
    /// the lower tail estimate (approximately 3.5 standard deviations under a normal distribution
    /// assumption).</description></item>
    /// <item><description>The loss probability is the absolute ratio of this boundary to the max loss:
    /// <c>|boundary / maxLoss|</c>. Values near 0 indicate low loss risk; values near or above 1 indicate high
    /// risk.</description></item>
    /// </list>
    /// </remarks>
    /// <param name="expectedPnlValues">The combined put and call spread expected P&amp;L values from Monte Carlo simulation.</param>
    /// <returns>A value between 0.0 and 1.0+ representing the estimated probability of breaching the max loss threshold.</returns>
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
