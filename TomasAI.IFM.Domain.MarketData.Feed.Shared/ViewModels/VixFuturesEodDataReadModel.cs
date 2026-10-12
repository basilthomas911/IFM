using MessagePack;
using Newtonsoft.Json;
using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

/// <summary>
/// Represents VIX futures end-of-day (EOD) market data for a contract and value date,
/// including OHLC prices and volume.
/// </summary>
/// <remarks>
/// MessagePack serializable. Only primitive members are serialized. The derived identifier
/// <see cref="EntityId"/> is excluded from serialization. Follows the same pattern as FundOrderReadModel.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record VixFuturesEodDataReadModel
{
    /// <summary>Full VIX futures contract identifier.</summary>
    [Key(0)]
    public string ContractId { get; init; }

    /// <summary>Trading (value) date for this EOD snapshot.</summary>
    [Key(1)]
    public DateOnly ValueDate { get; init; }

    /// <summary>Open price.</summary>
    [Key(2)]
    public decimal OpenPrice { get; init; }

    /// <summary>High price.</summary>
    [Key(3)]
    public decimal HighPrice { get; init; }

    /// <summary>Low price.</summary>
    [Key(4)]
    public decimal LowPrice { get; init; }

    /// <summary>Close (settlement) price.</summary>
    [Key(5)]
    public decimal ClosePrice { get; init; }

    /// <summary>Trading volume.</summary>
    [Key(6)]
    public long Volume { get; init; }

    /// <summary>
    /// Composite entity identifier (not serialized).
    /// </summary>
    [JsonIgnore]
    [IgnoreMember]
    public VixFuturesEodDataEntityId EntityId => new(ContractId ?? string.Empty, ValueDate);

    /// <summary>
    /// Parameterless constructor required for MessagePack and tooling.
    /// </summary>
    public VixFuturesEodDataReadModel() { }

    /// <summary>
    /// Initializes a VIX futures EOD snapshot with OHLC and volume.
    /// </summary>
    public VixFuturesEodDataReadModel(
        string contractId,
        DateOnly valueDate,
        decimal openPrice,
        decimal highPrice,
        decimal lowPrice,
        decimal closePrice,
        long volume)
    {
        ContractId = contractId;
        ValueDate = valueDate;
        OpenPrice = openPrice;
        HighPrice = highPrice;
        LowPrice = lowPrice;
        ClosePrice = closePrice;
        Volume = volume;
    }

    /// <summary>
    /// Returns a compact JSON representation (diagnostics/logging).
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}
