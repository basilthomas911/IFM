using MessagePack;
using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.MarketData.ViewModels;

/// <summary>
/// MessagePack-serializable view model representing a single point on a normal distribution curve,
/// specified by a standard deviation index and its corresponding percentage.
/// </summary>
/// <remarks>
/// Pattern mirrors FundOrderReadModel: explicit properties with sequential MessagePack keys;
/// JSON ToString retained for diagnostics.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record NormalCurveDataReadModel
{
    /// <summary>Standard deviation index (e.g., -3.0 to +3.0).</summary>
    [Key(0)]
    public double StdDevIndex { get; init; }

    /// <summary>Percentage (probability or frequency) at the given standard deviation index.</summary>
    [Key(1)]
    public double Percent { get; init; }

    /// <summary>Parameterless constructor for serializers.</summary>
    public NormalCurveDataReadModel() { }

    /// <summary>Creates a new normal curve data point.</summary>
    /// <param name="stdDevIndex">Standard deviation index.</param>
    /// <param name="percent">Percentage value.</param>
    public NormalCurveDataReadModel(double stdDevIndex, double percent)
    {
        StdDevIndex = stdDevIndex;
        Percent = percent;
    }

    /// <summary>Returns a compact JSON representation of the model.</summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);

    [JsonIgnore]
    [IgnoreMember]
    public bool IsValid => Percent > 0;
}