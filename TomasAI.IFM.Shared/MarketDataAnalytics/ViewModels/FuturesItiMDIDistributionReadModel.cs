using MessagePack;
using MathNet.Numerics.Distributions;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

/// <summary>
/// Represents distribution statistics (mean and standard deviation) of the Futures ITI Market Direction Indicator (MDI)
/// separated by intrinsic time trend (UpTrend vs DownTrend). Provides derived limits and classification helpers.
/// </summary>
/// <remarks>
/// The view model is MessagePack serializable and captures only the primitive distribution metrics required to classify
/// new MDI observations into trend regimes. Limits are derived (not serialized). Use <see cref="GetTrendType(double)"/>
/// or <see cref="GetTrendType(double,double)"/> to classify an observation.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public class FuturesItiMDIDistributionReadModel
{
    /// <summary>Mean MDI value observed during up-trend regimes.</summary>
    [Key(0)]
    public double UpTrendMean { get; set; }

    /// <summary>Standard deviation of MDI values during up-trend regimes.</summary>
    [Key(1)]
    public double UpTrendStdDev { get; set; }

    /// <summary>Mean MDI value observed during down-trend regimes.</summary>
    [Key(2)]
    public double DownTrendMean { get; set; }

    /// <summary>Standard deviation of MDI values during down-trend regimes.</summary>
    [Key(3)]
    public double DownTrendStdDev { get; set; }

    /// <summary>
    /// Upper limit at or above which the MDI is considered UpTrending. Computed from mean and a bounded volatility
    /// adjustment (uses σ or √σ if σ &gt; 1) and clipped into [1,99].
    /// </summary>
    [IgnoreMember]
    public double UpTrendingLimit
        => Math.Max(Math.Min(UpTrendMean + (UpTrendStdDev <= 1.0 ? UpTrendStdDev : Math.Sqrt(UpTrendStdDev)), 99), 1);

    /// <summary>
    /// Lower limit at or below which the MDI is considered DownTrending. Computed from mean and a bounded volatility
    /// adjustment (uses σ or √σ if σ &gt; 1) and clipped into [1,99].
    /// </summary>
    [IgnoreMember]
    public double DownTrendingLimit
        => Math.Min(Math.Max(DownTrendMean - (DownTrendStdDev <= 1.0 ? DownTrendStdDev : Math.Sqrt(DownTrendStdDev)), 1), 99);

    /// <summary>
    /// Creates distribution statistics from a collection of ITI signal MDI observations partitioned by intrinsic trend type.
    /// </summary>
    /// <param name="futuresItiSignalMDIs">Collection of ITI MDI observations. Cannot be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="futuresItiSignalMDIs"/> is null.</exception>
    public FuturesItiMDIDistributionReadModel(FuturesItiSignalMDIV2ReadModel[] futuresItiSignalMDIs)
    {
        IsArgumentNull.Set(futuresItiSignalMDIs);

        var upTrend = futuresItiSignalMDIs.Where(e => e.TrendType == IntrinsicTimeTrendType.UpTrend).Select(e => e.MDI).ToList();
        var downTrend = futuresItiSignalMDIs.Where(e => e.TrendType == IntrinsicTimeTrendType.DownTrend).Select(e => e.MDI).ToList();

        // Use MathNet estimate for consistency; fall back to defaults if a bucket is empty.
        if (upTrend.Count > 0)
        {
            var up = Normal.Estimate(upTrend);
            UpTrendMean = up.Mean;
            UpTrendStdDev = up.StdDev;
        }
        else
        {
            UpTrendMean = 80;
            UpTrendStdDev = 5;
        }

        if (downTrend.Count > 0)
        {
            var down = Normal.Estimate(downTrend);
            DownTrendMean = down.Mean;
            DownTrendStdDev = down.StdDev;
        }
        else
        {
            DownTrendMean = 20;
            DownTrendStdDev = 5;
        }
    }

    /// <summary>
    /// Parameterless constructor for MessagePack and unit test usage. Initializes with default synthetic distribution values.
    /// </summary>
    public FuturesItiMDIDistributionReadModel()
    {
        DownTrendMean = 20;
        DownTrendStdDev = 5;
        UpTrendMean = 80;
        UpTrendStdDev = 5;
    }

    /// <summary>
    /// Classifies an MDI value into a trend regime using dynamic limits.
    /// </summary>
    /// <param name="mdi">The MDI value to classify.</param>
    /// <returns>Computed <see cref="FuturesMDITrendType"/> regime.</returns>
    public FuturesMDITrendType GetTrendType(double mdi)
        => mdi switch
        {
            _ when mdi >= UpTrendingLimit => FuturesMDITrendType.UpTrending,
            _ when mdi <= DownTrendingLimit => FuturesMDITrendType.DownTrending,
            _ => FuturesMDITrendType.RangeBound
        };

    /// <summary>
    /// Classifies an MDI value into a trend regime incorporating an RSI confirmation filter.
    /// </summary>
    /// <param name="mdi">The MDI value to classify.</param>
    /// <param name="rsi">Relative Strength Index value used for confirmation.</param>
    /// <returns>Computed <see cref="FuturesMDITrendType"/> regime.</returns>
    public FuturesMDITrendType GetTrendType(double mdi, double rsi)
        => mdi switch
        {
            _ when mdi >= UpTrendingLimit && rsi >= 55 => FuturesMDITrendType.UpTrending,
            _ when mdi <= DownTrendingLimit && rsi <= 45 => FuturesMDITrendType.DownTrending,
            _ => FuturesMDITrendType.RangeBound
        };
}