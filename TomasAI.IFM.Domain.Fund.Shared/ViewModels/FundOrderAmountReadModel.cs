using MessagePack;

namespace TomasAI.IFM.Domain.Fund.Shared.ViewModels;

/// <summary>
/// MessagePack-serializable view model representing the amount allocated to an order for a given fund and value date.
/// </summary>
/// <remarks>
/// Pattern mirrors FundOrderReadModel: explicit properties with sequential MessagePack keys and
/// a parameterless constructor for serializers.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FundOrderAmountReadModel
{
    /// <summary>The fund identifier.</summary>
    [Key(0)]
    public int FundId { get; init; }

    /// <summary>The value (as-of) date for the amount.</summary>
    [Key(1)]
    public DateOnly ValueDate { get; init; }

    /// <summary>The order identifier within the fund.</summary>
    [Key(2)]
    public int OrderId { get; init; }

    /// <summary>The monetary amount allocated to the order.</summary>
    [Key(3)]
    public decimal Amount { get; init; }

    /// <summary>Parameterless constructor for MessagePack and other serializers.</summary>
    public FundOrderAmountReadModel() { }

    /// <summary>
    /// Full constructor to create a fund order amount snapshot.
    /// </summary>
    /// <param name="fundId">Fund identifier.</param>
    /// <param name="valueDate">As-of (value) date.</param>
    /// <param name="orderId">Order identifier.</param>
    /// <param name="amount">Allocated amount.</param>
    public FundOrderAmountReadModel(int fundId, DateOnly valueDate, int orderId, decimal amount)
    {
        FundId = fundId;
        ValueDate = valueDate;
        OrderId = orderId;
        Amount = amount;
    }

    [IgnoreMember]
    public bool IsValid => FundId > 0 && OrderId > 0 && ValueDate > DateOnly.MinValue;
}