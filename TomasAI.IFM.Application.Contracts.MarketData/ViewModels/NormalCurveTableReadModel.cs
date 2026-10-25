using MessagePack;
using Newtonsoft.Json;
using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

/// <summary>
/// Represents a normal curve lookup table used to derive probabilities and standard deviations
/// for option spread analytics based on Bollinger band positioning.
/// </summary>
/// <remarks>
/// MessagePack serializable. Stores an array of <see cref="NormalCurveDataReadModel"/> entries representing
/// cumulative percentages across standard deviation indices. Provides helper methods to compute put/call spread
/// probabilities and corresponding standard deviations given an asset price and Bollinger bands.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record NormalCurveTableReadModel
{
    /// <summary>
    /// Normal curve table where index corresponds to a percentile position within the band
    /// and <see cref="NormalCurveDataReadModel.Percent"/> contains the percentage value.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve array-valued serialized message and query contracts, including their MessagePack slots.")]
    [Key(0)]
    public NormalCurveDataReadModel[] NormalCurveTable { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack and tooling.
    /// </summary>
    public NormalCurveTableReadModel() { }

    /// <summary>
    /// Initializes the normal curve table with the provided entries.
    /// </summary>
    /// <param name="normalCurveTable">Array of normal curve entries.</param>
    public NormalCurveTableReadModel(NormalCurveDataReadModel[] normalCurveTable)
    {
        NormalCurveTable = normalCurveTable;
    }

    /// <summary>
    /// Computes the put option spread probability based on asset position relative to Bollinger bands.
    /// </summary>
    /// <param name="assetPrice">Current asset price.</param>
    /// <param name="upperBand">Bollinger upper band.</param>
    /// <param name="lowerBand">Bollinger lower band.</param>
    /// <returns>Probability in [0,1].</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Validation collects failure details instead of throwing; compatibility lookup helpers preserve their documented fallback when input data is unavailable.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S1244", Justification = "Exact equality verifies lossless decimal/double representation or a zero-width denominator sentinel; epsilon comparison would change validation.")]
    public double GetPutOptionSpreadProbability(double assetPrice, double upperBand, double lowerBand)
    {
        var optionSpreadProb = 0.0;
        try
        {
            if (assetPrice > upperBand)
                optionSpreadProb = NormalCurveTable[0].Percent * 2.0 / 100.0;
            else if (assetPrice < lowerBand)
                optionSpreadProb = NormalCurveTable[^1].Percent * 2.0 / 100.0;
            else if ((upperBand - lowerBand) != 0.0)
            {
                var index = System.Convert.ToInt32((upperBand - assetPrice) / (upperBand - lowerBand) * 100);
                optionSpreadProb = NormalCurveTable[index].Percent * 2.0 / 100.0;
            }
        }
        catch { /* Preserve the existing default probability/deviation when lookup inputs are unavailable. */ }
        return optionSpreadProb;
    }

    /// <summary>
    /// Computes the put option spread standard deviation adjustment.
    /// </summary>
    /// <param name="assetPrice">Current asset price.</param>
    /// <param name="upperBand">Bollinger upper band.</param>
    /// <param name="lowerBand">Bollinger lower band.</param>
    /// <returns>Standard deviation adjustment in [1,2].</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Validation collects failure details instead of throwing; compatibility lookup helpers preserve their documented fallback when input data is unavailable.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Preserve the established instance event, read-model or validation API used by existing consumers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S1244", Justification = "Exact equality verifies lossless decimal/double representation or a zero-width denominator sentinel; epsilon comparison would change validation.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Preserve the established instance event, read-model or validation API used by existing consumers.")]
    public double GetPutOptionSpreadStdDev(double assetPrice, double upperBand, double lowerBand)
    {
        var stdDev = 1.0;
        try
        {
            if (assetPrice > upperBand)
                stdDev = 1.0;
            else if (assetPrice < lowerBand)
                stdDev = 2.0;
            else if ((upperBand - lowerBand) != 0.0)
                stdDev = 1.0 + (upperBand - assetPrice) / (upperBand - lowerBand);
        }
        catch { /* Preserve the existing default probability/deviation when lookup inputs are unavailable. */ }
        return stdDev;
    }

    /// <summary>
    /// Computes the call option spread probability based on asset position relative to Bollinger bands.
    /// </summary>
    /// <param name="assetPrice">Current asset price.</param>
    /// <param name="upperBand">Bollinger upper band.</param>
    /// <param name="lowerBand">Bollinger lower band.</param>
    /// <returns>Probability in [0,1].</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Validation collects failure details instead of throwing; compatibility lookup helpers preserve their documented fallback when input data is unavailable.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S1244", Justification = "Exact equality verifies lossless decimal/double representation or a zero-width denominator sentinel; epsilon comparison would change validation.")]
    public double GetCallOptionSpreadProbability(double assetPrice, double upperBand, double lowerBand)
    {
        var optionSpreadProb = 0.0;
        try
        {
            if (assetPrice < lowerBand)
                optionSpreadProb = NormalCurveTable[0].Percent * 2.0 / 100.0;
            else if (assetPrice > upperBand)
                optionSpreadProb = NormalCurveTable[^1].Percent * 2.0 / 100.0;
            else if ((upperBand - lowerBand) != 0.0)
            {
                var index = System.Convert.ToInt32((assetPrice - lowerBand) / (upperBand - lowerBand) * 100);
                optionSpreadProb = NormalCurveTable[index].Percent * 2.0 / 100.0;
            }
        }
        catch { /* Preserve the existing default probability/deviation when lookup inputs are unavailable. */ }
        return optionSpreadProb;
    }

    /// <summary>
    /// Computes the call option spread standard deviation adjustment.
    /// </summary>
    /// <param name="assetPrice">Asset price.</param>
    /// <param name="upperBand">Bollinger upper band.</param>
    /// <param name="lowerBand">Bollinger lower band.</param>
    /// <returns>Standard deviation adjustment in [1,2].</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Validation collects failure details instead of throwing; compatibility lookup helpers preserve their documented fallback when input data is unavailable.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Preserve the established instance event, read-model or validation API used by existing consumers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S1244", Justification = "Exact equality verifies lossless decimal/double representation or a zero-width denominator sentinel; epsilon comparison would change validation.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Preserve the established instance event, read-model or validation API used by existing consumers.")]
    public double GetCallOptionSpreadStdDev(double assetPrice, double upperBand, double lowerBand)
    {
        var stdDev = 0.0;
        try
        {
            if (assetPrice < lowerBand)
                stdDev = 1.0;
            else if (assetPrice > upperBand)
                stdDev = 2.0;
            else if ((upperBand - lowerBand) != 0.0)
                stdDev = 1.0 + (assetPrice - lowerBand) / (upperBand - lowerBand);
        }
        catch { /* Preserve the existing default probability/deviation when lookup inputs are unavailable. */ }
        return stdDev;
    }

    /// <summary>
    /// Returns a compact JSON representation (diagnostics/logging).
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);

    [JsonIgnore]
    [IgnoreMember]
    public bool IsValid => NormalCurveTable != null && NormalCurveTable.Length > 0;
}
