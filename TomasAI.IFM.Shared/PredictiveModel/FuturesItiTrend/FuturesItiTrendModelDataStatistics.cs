using MessagePack;
using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend;

/// <summary>
/// MessagePack-serializable statistics for an ITI trend model data sample.
/// </summary>
/// <remarks>
/// Pattern mirrors FundOrderReadModel: explicit MessagePack keys and a parameterless constructor for serializers.
/// </remarks>
/// <param name="Count">Number of observations.</param>
/// <param name="Maximum">Maximum observed value.</param>
/// <param name="Mean">Arithmetic mean of the sample.</param>
/// <param name="Median">Median of the sample.</param>
/// <param name="Minimum">Minimum observed value.</param>
/// <param name="Skewness">Skewness of the distribution.</param>
/// <param name="StdDev">Standard deviation.</param>
/// <param name="Variance">Variance.</param>
[MessagePackObject(AllowPrivate = true)]
public readonly record struct FuturesItiTrendModelDataStatistics(
    [property: Key(0)] int Count,
    [property: Key(1)] double Maximum,
    [property: Key(2)] double Mean,
    [property: Key(3)] double Median,
    [property: Key(4)] double Minimum,
    [property: Key(5)] double Skewness,
    [property: Key(6)] double StdDev,
    [property: Key(7)] double Variance)
{
    /// <summary>
    /// Parameterless constructor for serializers; initializes all values to zero.
    /// </summary>
    public FuturesItiTrendModelDataStatistics() : this(0, 0, 0, 0, 0, 0, 0, 0) { }

    /// <summary>
    /// Returns a compact JSON representation of this statistics object.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);

    /// <summary>
    /// An empty statistics instance with all fields set to zero.
    /// </summary>
    public static FuturesItiTrendModelDataStatistics Empty => new(0, 0, 0, 0, 0, 0, 0, 0);
}