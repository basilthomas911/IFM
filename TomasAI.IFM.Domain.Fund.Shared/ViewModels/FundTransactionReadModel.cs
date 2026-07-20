using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Domain.Fund.Shared.ViewModels;

/// <summary>
/// Represents a view model for a fund transaction, encapsulating details such as transaction type, associated
/// identifiers, financial amounts, and status information.
/// </summary>
/// <remarks>This record is designed to provide a structured representation of a fund transaction, including
/// metadata such as transaction date, type, and associated entities (e.g., fund, order, and trade). It supports
/// serialization via MessagePack and JSON for efficient data transfer and storage.</remarks>
[MessagePackObject(AllowPrivate = true)]
public record FundTransactionReadModel
{
    // Serializable members (stable numeric keys)
    [Key(0)]
    public long TransactionId { get; init; }
    [Key(1)]
    public DateTime TransactionDate { get; init; }
    [Key(2)]
    public FundTransactionType TransactionType { get; init; }
    [Key(3)]
    public int FundId { get; init; }
    [Key(4)]
    public int OrderId { get; init; }
    [Key(5)]
    public int TradeId { get; init; }
    [Key(6)]
    public TradeType TradeType { get; init; }
    [Key(7)]
    public DateOnly ValueDate { get; init; }
    [Key(8)]
    public TradeStatus TradeStatus { get; init; }
    [Key(9)]
    public string Description { get; init; }
    [Key(10)]
    public decimal Amount { get; init; }
    [Key(11)]
    public decimal Balance { get; init; }

    // Ctor used by code and by MessagePack (matches keys order)
    public FundTransactionReadModel(
        long transactionId,
        DateTime transactionDate,
        FundTransactionType transactionType,
        int fundId,
        int orderId,
        int tradeId,
        TradeType tradeType,
        DateOnly valueDate,
        TradeStatus tradeStatus,
        string description,
        decimal amount,
        decimal balance)
    {
        TransactionId = transactionId;
        TransactionDate = transactionDate;
        TransactionType = transactionType;
        FundId = fundId;
        OrderId = orderId;
        TradeId = tradeId;
        TradeType = tradeType;
        ValueDate = valueDate;
        TradeStatus = tradeStatus;
        Description = description;
        Amount = amount;
        Balance = balance;
    }

    // Derived/computed members - ignored by MessagePack
    [JsonIgnore]
    [IgnoreMember]
    public FundTransactionId Id => new(FundId, ValueDate, OrderId, TradeId, TradeType, TransactionType, TransactionDate, TransactionId);

    [JsonIgnore]
    [IgnoreMember]
    public FundTransactionEntityId EntityId => new(FundId, OrderId);

    [JsonIgnore]
    [IgnoreMember]
    public bool IsValid => TransactionId > 0 && FundId > 0;

    public override string ToString() => JsonConvert.SerializeObject(this);
    public override int GetHashCode() => $"{this}".GetHashCode();

    // Factory helpers (unchanged API)
    public static FundTransactionReadModel AsOpeningTradeTransaction(int fundId, int orderId, int tradeId, TradeType tradeType, DateOnly valueDate, string description, decimal amount)
       => new(
           transactionId: 0,
           transactionDate: DateTime.Now,
           transactionType: FundTransactionType.OpeningTrade,
           fundId: fundId,
           orderId: orderId,
           tradeId: tradeId,
           tradeType: tradeType,
           valueDate: valueDate,
           tradeStatus: TradeStatus.Open,
           description: description,
           amount: amount,
           balance: 0
       );

    public static FundTransactionReadModel AsTradeCommissionTransaction(int fundId, int orderId, int tradeId, TradeType tradeType, DateOnly valueDate, TradeStatus tradeStatus, string description, decimal amount)
        => new(
            transactionId: 0,
            transactionDate: DateTime.Now,
            transactionType: FundTransactionType.TradeCommission,
            fundId: fundId,
            orderId: orderId,
            tradeId: tradeId,
            tradeType: tradeType,
            valueDate: valueDate,
            tradeStatus: tradeStatus,
            description: description,
            amount: amount,
            balance: 0
        );

    public static FundTransactionReadModel AsRealizedTradePnlTransaction(int fundId, int orderId, int tradeId, TradeType tradeType, DateOnly valueDate, string description, decimal amount)
         => new(
             transactionId: 0,
             transactionDate: DateTime.Now,
             transactionType: FundTransactionType.RealizedTradePnl,
             fundId: fundId,
             orderId: orderId,
             tradeId: tradeId,
             tradeType: tradeType,
             valueDate: valueDate,
             tradeStatus: TradeStatus.Close,
             description: description,
             amount: amount,
             balance: 0
         );

    public static FundTransactionReadModel AsUnrealizedTradePnlTransaction(int fundId, int orderId, int tradeId, TradeType tradeType, DateOnly valueDate, string description, decimal amount)
        => new(
            transactionId: 0,
            transactionDate: DateTime.Now,
            transactionType: FundTransactionType.UnrealizedTradePnl,
            fundId: fundId,
            orderId: orderId,
            tradeId: tradeId,
            tradeType: tradeType,
            valueDate: valueDate,
            tradeStatus: TradeStatus.EndOfDay,
            description: description,
            amount: amount,
            balance: 0
        );

    public static FundTransactionReadModel AsAdjustmentTransaction(FundTransactionType adjustmentTransactionType, int fundId, int orderId, int tradeId, TradeType tradeType, DateOnly valueDate, TradeStatus tradeStatus, string description, decimal amount, decimal balance)
       => new(
           transactionId: 0,
           transactionDate: DateTime.Now,
           transactionType: adjustmentTransactionType,
           fundId: fundId,
           orderId: orderId,
           tradeId: tradeId,
           tradeType: tradeType,
           valueDate: valueDate,
           tradeStatus: tradeStatus,
           description: description,
           amount: amount,
           balance: balance
       );

    public static FundTransactionReadModel AsEndOfDayProcessedTransaction(int fundId, int orderId, int tradeId, TradeType tradeType, DateOnly valueDate, string description, decimal amount)
       => new(
           transactionId: 0,
           transactionDate: DateTime.Now,
           transactionType: FundTransactionType.EndOfDayProcessed,
           fundId: fundId,
           orderId: orderId,
           tradeId: tradeId,
           tradeType: tradeType,
           valueDate: valueDate,
           tradeStatus: TradeStatus.EndOfDay,
           description: description,
           amount: amount,
           balance: 0
       );
}
