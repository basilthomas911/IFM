using MessagePack;
using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

/// <summary>
/// MessagePack-serializable view model representing averaged ITI signal metrics for a futures contract
/// on a specific value date (e.g., predicted delta and RSI).
/// </summary>
/// <remarks>
/// Pattern mirrors FundOrderReadModel: explicit properties with sequential MessagePack keys
/// and a parameterless constructor for serializers.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesItiSignalAverageInfoDataModel
{
    /// <summary>Full futures contract identifier.</summary>
    [Key(0)]
    public string ContractId { get; init; } = string.Empty;

    /// <summary>As-of (value) date for the averaged signal metrics.</summary>
    [Key(1)]
    public DateOnly ValueDate { get; init; }

    /// <summary>Predicted delta value from the ITI model.</summary>
    [Key(2)]
    public double PredictedDelta { get; init; }

    /// <summary>Computed RSI value for the futures series.</summary>
    [Key(3)]
    public double FuturesRSI { get; init; }

    /// <summary>Parameterless constructor for serializers.</summary>
    public FuturesItiSignalAverageInfoDataModel() { }

    /// <summary>
    /// Full constructor to create an averaged ITI signal snapshot.
    /// </summary>
    /// <param name="contractId">Futures contract identifier.</param>
    /// <param name="valueDate">As-of (value) date.</param>
    /// <param name="predictedDelta">Predicted delta metric.</param>
    /// <param name="futuresRSI">RSI metric for the futures series.</param>
    public FuturesItiSignalAverageInfoDataModel(string contractId, DateOnly valueDate, double predictedDelta, double futuresRSI)
    {
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        PredictedDelta = predictedDelta;
        FuturesRSI = futuresRSI;
    }

    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}