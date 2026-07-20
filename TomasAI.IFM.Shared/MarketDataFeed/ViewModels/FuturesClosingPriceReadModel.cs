using MessagePack;

namespace TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

/// <summary>
/// MessagePack-serializable view model representing a futures contract closing price snapshot for a specific value date.
/// </summary>
/// <remarks>
/// Pattern mirrors other MessagePack-compatible view models in the project:
/// - Explicit properties annotated with sequential <see cref="KeyAttribute"/> indices.
/// - A parameterless constructor for serializers.
/// - A full constructor annotated with <see cref="SerializationConstructorAttribute"/> for deserialization.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesClosingPriceReadModel
{
    /// <summary>
    /// Full futures contract identifier (root + contract month/year code).
    /// </summary>
    [Key(0)]
    public string ContractId { get; init; } 

    /// <summary>
    /// As-of (value) date for this closing price.
    /// </summary>
    [Key(1)]
    public DateOnly ValueDate { get; init; }

    /// <summary>
    /// Closing price for the contract on <see cref="ValueDate"/>.
    /// </summary>
    [Key(2)]
    public decimal ClosingPrice { get; init; }

    /// <summary>
    /// Timestamp when this record was created.
    /// </summary>
    [Key(3)]
    public DateTime CreatedOn { get; init; }

    /// <summary>
    /// Identity of the creator of this record.
    /// </summary>
    [Key(4)]
    public string CreatedBy { get; init; } 

    /// <summary>
    /// Derived identifier composed from <see cref="ContractId"/> and <see cref="ValueDate"/>.
    /// Excluded from MessagePack via <see cref="IgnoreMemberAttribute"/>.
    /// </summary>
    [IgnoreMember]
    public FuturesDataId Id => FuturesDataId.Create(ContractId, ValueDate);

    /// <summary>
    /// Parameterless constructor for serializers.
    /// </summary>
    public FuturesClosingPriceReadModel() { }

    /// <summary>
    /// MessagePack serialization constructor (Key indices must match property Key attributes).
    /// </summary>
    /// <param name="contractId">Contract identifier (Key 0).</param>
    /// <param name="valueDate">Value date (Key 1).</param>
    /// <param name="closingPrice">Closing price (Key 2).</param>
    /// <param name="createdOn">Creation timestamp (Key 3).</param>
    /// <param name="createdBy">Record creator (Key 4).</param>
    [SerializationConstructor]
    public FuturesClosingPriceReadModel(
        string contractId,
        DateOnly valueDate,
        decimal closingPrice,
        DateTime createdOn,
        string createdBy)
    {
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        ClosingPrice = closingPrice;
        CreatedOn = createdOn;
        CreatedBy = createdBy ?? string.Empty;
    }
}