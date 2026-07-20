using System.Collections.Immutable;
using MessagePack;
using Newtonsoft.Json;

namespace TomasAI.IFM.Domain.Fund.Shared.ViewModels;

/// <summary>
/// Represents a view model for a fund order, encapsulating details such as the fund, order, trade, and maturity dates,
/// as well as metadata about the order's creation and updates.
/// </summary>
/// <remarks>This record is designed to provide a structured representation of a fund order, including its unique
/// identifiers, status, associated contract, and audit information. It also includes derived properties for validation
/// and composite identification.</remarks>
[MessagePackObject(AllowPrivate = true)]
public record FundOrderReadModel
{
    [Key(0)]
    public int FundId { get; init; }
    [Key(1)]
    public int OrderId { get; init; }   
    [Key(2)]
    public DateTime OrderDate { get; init; }
    [Key(3)]
    public OrderStatus OrderStatus { get; init; }
    [Key(4)]
    public string BaseContractId { get; init; }
    [Key(5)]
    public DateOnly TradeDate { get; init; }
    [Key(6)]
    public DateOnly MaturityDate { get; init; }
    [Key(7)]
    public string Reference { get; init; }
    [Key(8)]
    public DateTime CreatedOn { get; init; }
    [Key(9)]
    public string CreatedBy { get; init; }
    [Key(10)]
    public DateTime? UpdatedOn { get; init; }
    [Key(11)]
    public string UpdatedBy { get; init; }


    public FundOrderReadModel(
        int fundId, 
        int orderId,
        DateTime orderDate,
        OrderStatus orderStatus,
        string baseContractId,
        DateOnly tradeDate,
        DateOnly maturityDate,
        string reference,
        DateTime createdOn,
        string createdBy,
        DateTime? updatedOn,
        string updatedBy)
    {
        FundId = fundId;
        OrderId = orderId;
        OrderDate = orderDate;
        OrderStatus = orderStatus;
        BaseContractId = baseContractId;
        TradeDate = tradeDate;
        MaturityDate = maturityDate;
        Reference = reference;
        CreatedOn = createdOn;
        CreatedBy = createdBy;
        UpdatedOn = updatedOn;
        UpdatedBy = updatedBy;
    }

    [JsonIgnore]
    [IgnoreMember]
    public bool IsValid => FundId > 0 && OrderId > 0;
    [JsonIgnore]
    [IgnoreMember]
    public FundOrderId Id => new (FundId, OrderId);
    [JsonIgnore]
    [IgnoreMember]
    List<FundOrderTradeReadModel>? _trades;
    [JsonProperty]
    [IgnoreMember]
    public ImmutableArray<FundOrderTradeReadModel> Trades => _trades == null ? [] : [.. _trades];
    public override string ToString() => JsonConvert.SerializeObject(this);

    /// <summary>
    /// Adds a new trade to the collection of fund order trades.
    /// </summary>
    /// <remarks>If the collection of trades is uninitialized, it will be initialized before adding the
    /// trade.</remarks>
    /// <param name="fundOrderTrade">The trade to add to the collection. Cannot be <see langword="null"/>.</param>
    public void Add(FundOrderTradeReadModel fundOrderTrade)
    {
        _trades ??= [];
        _trades.Add(fundOrderTrade);
    }

}
