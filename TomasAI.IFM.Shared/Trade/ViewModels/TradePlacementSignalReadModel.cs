using MessagePack;
using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.Trade.ViewModels;

/// <summary>
/// MessagePack-serializable view model representing a trade placement signal snapshot,
/// including sequence, contract, value date, signal, price, and audit fields.
/// </summary>
/// <remarks>
/// Pattern mirrors FundOrderReadModel: explicit properties with sequential MessagePack keys
/// and a parameterless constructor for serializers.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record TradePlacementSignalReadModel
{
    /// <summary>Unique sequence identifier for this signal.</summary>
    [Key(0)]
    public long SequenceId { get; init; }

    /// <summary>Underlying contract identifier.</summary>
    [Key(1)]
    public string ContractId { get; init; } = string.Empty;

    /// <summary>As-of (value) date for the signal.</summary>
    [Key(2)]
    public DateOnly ValueDate { get; init; }

    /// <summary>The trade placement signal classification.</summary>
    [Key(3)]
    public TradePlacementSignalType TradePlacementSignal { get; init; }

    /// <summary>Associated trade price for the signal.</summary>
    [Key(4)]
    public decimal TradePrice { get; init; }

    /// <summary>Creation timestamp.</summary>
    [Key(5)]
    public DateTime CreatedOn { get; init; }

    /// <summary>Creator identity.</summary>
    [Key(6)]
    public string CreatedBy { get; init; } = string.Empty;

    /// <summary>Parameterless constructor for serializers.</summary>
    public TradePlacementSignalReadModel() { }

    /// <summary>
    /// Full constructor to initialize a trade placement signal snapshot.
    /// </summary>
    /// <param name="SequenceId">Unique sequence identifier.</param>
    /// <param name="ContractId">Underlying contract identifier.</param>
    /// <param name="ValueDate">As-of (value) date.</param>
    /// <param name="TradePlacementSignal">Trade placement signal classification.</param>
    /// <param name="TradePrice">Associated trade price.</param>
    /// <param name="CreatedOn">Creation timestamp.</param>
    /// <param name="CreatedBy">Creator identity.</param>
    public TradePlacementSignalReadModel(
        long sequenceId,
        string contractId,
        DateOnly valueDate,
        TradePlacementSignalType tradePlacementSignal,
        decimal tradePrice,
        DateTime createdOn,
        string createdBy)
    {
        SequenceId = sequenceId;
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        TradePlacementSignal = tradePlacementSignal;
        TradePrice = tradePrice;
        CreatedOn = createdOn;
        CreatedBy = createdBy ?? string.Empty;
    }

    /// <summary>Returns a compact JSON representation.</summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);

    [JsonIgnore]
    [IgnoreMember]
    public bool IsValid => SequenceId > 0 && !string.IsNullOrEmpty(ContractId) && ValueDate > DateOnly.MinValue;
}