using FluentValidation;
using FluentValidation.Results;
using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

/// <summary>
/// Represents a futures bar (OHLCV-like) data point enriched with model triggers,
/// suitable for storage and transport in analytics and feed bounded contexts.
/// </summary>
/// <remarks>
/// MessagePack serializable. Only primitive and enum members are serialized. The derived identifier
/// <see cref="Id"/> is excluded from serialization.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesBarDataReadModel
{
    /// <summary>Full futures contract identifier (root + contract month/year code).</summary>
    [Key(0)]
    public string ContractId { get; init; }

    /// <summary>Symbol root associated with the contract (e.g., ES, NQ, CL).</summary>
    [Key(1)]
    public string Symbol { get; init; }

    /// <summary>Value (trading) date the bar belongs to.</summary>
    [Key(2)]
    public DateOnly ValueDate { get; init; }

    /// <summary>Actual timestamp of the bar within the trading session.</summary>
    [Key(3)]
    public DateTime BarDate { get; init; }

    /// <summary>Granularity/rate of the bar (e.g., Minute).</summary>
    [Key(4)]
    public BarRateType BarRateType { get; init; }

    /// <summary>Numeric value of the bar (e.g., close or aggregated metric).</summary>
    [Key(5)]
    public decimal BarValue { get; init; }

    /// <summary>Computed trigger threshold for initiating an up-trend action.</summary>
    [Key(6)]
    public double UpTrendTrigger { get; init; }

    /// <summary>Computed trigger threshold for initiating a down-trend action.</summary>
    [Key(7)]
    public double DownTrendTrigger { get; init; }

    /// <summary>
    /// Composite identifier (ContractId + Symbol + ValueDate). Not serialized.
    /// </summary>
    [JsonIgnore]
    [IgnoreMember]
    public FuturesBarDataId Id => new(ContractId ?? string.Empty, Symbol ?? string.Empty, ValueDate);

    /// <summary>
    /// Parameterless constructor required for MessagePack and tooling.
    /// </summary>
    public FuturesBarDataReadModel() { }

    /// <summary>
    /// Full constructor initializing all serialized properties.
    /// </summary>
    /// <param name="contractId">Full futures contract identifier.</param>
    /// <param name="symbol">Symbol root.</param>
    /// <param name="valueDate">Trading date of the bar.</param>
    /// <param name="barDate">Timestamp of the bar within the session.</param>
    /// <param name="barRateType">Bar granularity.</param>
    /// <param name="barValue">Bar value (e.g., close).</param>
    /// <param name="upTrendTrigger">Up-trend trigger threshold.</param>
    /// <param name="downTrendTrigger">Down-trend trigger threshold.</param>
    public FuturesBarDataReadModel(
        string contractId,
        string symbol,
        DateOnly valueDate,
        DateTime barDate,
        BarRateType barRateType,
        decimal barValue,
        double upTrendTrigger,
        double downTrendTrigger)
    {
        ContractId = contractId;
        Symbol = symbol;
        ValueDate = valueDate;
        BarDate = barDate;
        BarRateType = barRateType;
        BarValue = barValue;
        UpTrendTrigger = upTrendTrigger;
        DownTrendTrigger = downTrendTrigger;
    }

    /// <summary>
    /// Returns a compact JSON representation (diagnostics/logging).
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}
