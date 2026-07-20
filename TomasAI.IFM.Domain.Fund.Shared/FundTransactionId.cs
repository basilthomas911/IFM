using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Domain.Fund.Shared;

/// <summary>
/// Represents a unique identifier for a fund transaction, encapsulating details such as the fund, trade, and
/// transaction metadata.
/// </summary>
/// <remarks>This record is used to uniquely identify a specific transaction within a fund's lifecycle. It
/// includes details such as the fund ID,  value date, associated order and trade IDs, trade type, transaction type,
/// transaction date, and a unique transaction ID.  The <see cref="IsValid"/> property can be used to verify the
/// validity of the identifier based on its components.</remarks>
[MessagePackObject(AllowPrivate = true)]
public record FundTransactionId
{
    [Key(0)]
    public int FundId { get; init; }
    [Key(1)]
    public DateOnly ValueDate { get; init; }
    [Key(2)]
    public int OrderId { get; init; }
    [Key(3)]
    public int TradeId { get; init; }
    [Key(4)]
    public TradeType TradeType { get; init; }
    [Key(5)]
    public FundTransactionType TransactionType { get; init; }
    [Key(6)]
    public DateTime TransactionDate { get; init; }
    [Key(7)]
    public long TransactionId { get; init; }

    public FundTransactionId(
        int fundId,
        DateOnly valueDate,
        int orderId,
        int tradeId,
        TradeType tradeType,
        FundTransactionType transactionType,
        DateTime transactionDate,
        long transactionId)
    {
        FundId = fundId;
        ValueDate = valueDate;
        OrderId = orderId;
        TradeId = tradeId;
        TradeType = tradeType;
        TransactionType = transactionType;
        TransactionDate = transactionDate;
        TransactionId = transactionId;
    }

    [JsonIgnore]
    [IgnoreMember]
    public bool IsValid => FundId > 0
        && OrderId > 0
        && TradeId > 0
        && TradeType != TradeType.Unknown
        && (ValueDate != DateOnly.MinValue && ValueDate != DateOnly.MaxValue)
        && (TransactionDate != DateTime.MinValue && TransactionDate != DateTime.MaxValue)
        && TransactionId > 0;

    public override string ToString() => JsonConvert.SerializeObject(this);
    public override int GetHashCode() => $"{this}".GetHashCode();
}