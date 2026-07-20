using MessagePack;

namespace TomasAI.IFM.Domain.Fund.Shared.ViewModels;

/// <summary>
/// MessagePack-serializable view model representing a fund's daily balance for a given value date.
/// </summary>
/// <remarks>
/// Pattern mirrors TradePlanForwardLossLimitReadModel: explicit properties with sequential MessagePack keys
/// and a parameterless constructor for serializers.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FundDailyBalanceReadModel
{
    /// <summary>The fund identifier.</summary>
    [Key(0)]
    public int FundId { get; init; }

    /// <summary>The value (as-of) date.</summary>
    [Key(1)]
    public DateOnly ValueDate { get; init; }

    /// <summary>The balance on the specified value date.</summary>
    [Key(2)]
    public decimal Balance { get; init; }

    /// <summary>Parameterless constructor for serializers.</summary>
    public FundDailyBalanceReadModel() { }

    /// <summary>
    /// Initializes a new daily balance snapshot.
    /// </summary>
    /// <param name="fundId">Fund identifier.</param>
    /// <param name="valueDate">As-of (value) date.</param>
    /// <param name="balance">Balance amount.</param>
    public FundDailyBalanceReadModel(int fundId, DateOnly valueDate, decimal balance)
    {
        FundId = fundId;
        ValueDate = valueDate;
        Balance = balance;
    }

    [IgnoreMember]
    public bool IsValid => FundId > 0 && ValueDate > DateOnly.MinValue;
}