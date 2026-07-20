using MessagePack;
using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.ViewModels;

/// <summary>
/// Represents coastline (regime persistence) counters for intrinsic time trend analysis of a futures contract.
/// </summary>
/// <remarks>
/// These counters track how many consecutive observations have been classified as UpTrend or DownTrend, and
/// can be used by predictive models to assess regime durability. The model is MessagePack serializable to
/// support low-latency transport and storage. A derived <see cref="TotalCount"/> property is excluded from
/// serialization.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesItiTrendCoastLineCountersReadModel
{
    /// <summary>
    /// Number of consecutive intrinsic time observations classified as an up trend.
    /// </summary>
    [Key(0)]
    public int UpTrendCount { get; init; }

    /// <summary>
    /// Number of consecutive intrinsic time observations classified as a down trend.
    /// </summary>
    [Key(1)]
    public int DownTrendCount { get; init; }

    /// <summary>
    /// Total of all counted trend observations (up + down). Not serialized.
    /// </summary>
    [IgnoreMember]
    public int TotalCount => UpTrendCount + DownTrendCount;

    /// <summary>
    /// Parameterless constructor required for MessagePack and tooling scenarios. Initializes all counters to zero.
    /// </summary>
    public FuturesItiTrendCoastLineCountersReadModel() { }

    /// <summary>
    /// Creates a new coastline counter snapshot.
    /// </summary>
    /// <param name="upTrendCount">Current consecutive up trend count.</param>
    /// <param name="downTrendCount">Current consecutive down trend count.</param>
    public FuturesItiTrendCoastLineCountersReadModel(int upTrendCount, int downTrendCount)
    {
        UpTrendCount = upTrendCount;
        DownTrendCount = downTrendCount;
    }

    /// <summary>
    /// Returns a JSON representation of the counters.
    /// </summary>
    public override string ToString()
        => JsonConvert.SerializeObject(this, Formatting.Indented);
}