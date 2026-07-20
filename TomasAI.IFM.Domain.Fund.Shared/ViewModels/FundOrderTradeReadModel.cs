using TomasAI.IFM.Shared.Trade;
using MessagePack;
using Newtonsoft.Json;

namespace TomasAI.IFM.Domain.Fund.Shared.ViewModels;

/// <summary>
/// MessagePack-serializable view model representing a trade within a fund order,
/// including identifiers, trade metadata, and helper methods for contract parsing.
/// </summary>
/// <remarks>
/// Pattern mirrors FundOrderReadModel: explicit properties with sequential MessagePack keys;
/// derived members are excluded from MessagePack via IgnoreMember/JsonIgnore.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FundOrderTradeReadModel
{
    /// <summary>Fund identifier.</summary>
    [Key(0)]
    public int FundId { get; init; }

    /// <summary>Order identifier within the fund.</summary>
    [Key(1)]
    public int OrderId { get; init; }

    /// <summary>Trade identifier within the order.</summary>
    [Key(2)]
    public int TradeId { get; init; }

    /// <summary>Strategy/type of the option trade.</summary>
    [Key(3)]
    public TradeType TradeType { get; init; }

    /// <summary>Trade execution date.</summary>
    [Key(4)]
    public DateOnly TradeDate { get; init; }

    /// <summary>Maturity/expiry date.</summary>
    [Key(5)]
    public DateOnly MaturityDate { get; init; }

    /// <summary>Lifecycle state of the trade.</summary>
    [Key(6)]
    public TradeState TradeState { get; init; }

    /// <summary>Trade action (Buy/Sell).</summary>
    [Key(7)]
    public TradeAction TradeAction { get; init; }

    /// <summary>Formatted reference string describing the structure (e.g., iron condor legs).</summary>
    [Key(8)]
    public string Reference { get; init; } = string.Empty;

    /// <summary>Indicates if this is the primary trade for the order.</summary>
    [Key(9)]
    public bool PrimaryTrade { get; init; }

    /// <summary>Base contract symbol used when parsing contract ids.</summary>
    [Key(10)]
    public string BaseContractSymbol { get; init; } = string.Empty;

    /// <summary>Creation timestamp (UTC preferred).</summary>
    [Key(11)]
    public DateTime CreatedOn { get; init; }

    /// <summary>User or system that created the record.</summary>
    [Key(12)]
    public string CreatedBy { get; init; } = string.Empty;

    /// <summary>Last updated timestamp, if any.</summary>
    [Key(13)]
    public DateTime? UpdatedOn { get; init; }

    /// <summary>User or system that last updated the record.</summary>
    [Key(14)]
    public string UpdatedBy { get; init; } = string.Empty;

    /// <summary>Parameterless constructor for serializers.</summary>
    public FundOrderTradeReadModel() { }

    /// <summary>
    /// Full constructor to initialize a fund order trade view model.
    /// </summary>
    public FundOrderTradeReadModel(
        int fundId,
        int orderId,
        int tradeId,
        TradeType tradeType,
        DateOnly tradeDate,
        DateOnly maturityDate,
        TradeState tradeState,
        TradeAction tradeAction,
        string reference,
        bool primaryTrade,
        string baseContractSymbol,
        DateTime createdOn,
        string createdBy,
        DateTime? updatedOn,
        string updatedBy)
    {
        FundId = fundId;
        OrderId = orderId;
        TradeId = tradeId;
        TradeType = tradeType;
        TradeDate = tradeDate;
        MaturityDate = maturityDate;
        TradeState = tradeState;
        TradeAction = tradeAction;
        Reference = reference ?? string.Empty;
        PrimaryTrade = primaryTrade;
        BaseContractSymbol = baseContractSymbol ?? string.Empty;
        CreatedOn = createdOn;
        CreatedBy = createdBy ?? string.Empty;
        UpdatedOn = updatedOn;
        UpdatedBy = updatedBy ?? string.Empty;
    }

    /// <summary>True when basic identifiers are set to positive values.</summary>
    [JsonIgnore]
    [IgnoreMember]
    public bool IsValid => FundId > 0 && OrderId > 0 && TradeId > 0;

    /// <summary>Derived identifier for this fund-order-trade (excluded from MessagePack).</summary>
    [JsonIgnore]
    [IgnoreMember]
    public FundOrderTradeId Id => new(FundId, OrderId, TradeId);

    /// <summary>Returns a JSON string representation of this view model.</summary>
    public override string ToString() => JsonConvert.SerializeObject(this);

    /// <summary>
    /// Extracts underlying contract ids from the <see cref="Reference"/> string based on the trade type.
    /// Currently supports Iron Condor patterns (P:put strikes, C:call strikes).
    /// </summary>
    public string[] GetContractIds()
    {
        var contractIds = new List<string>();
        if (!string.IsNullOrWhiteSpace(Reference))
        {
            switch (TradeType)
            {
                case TradeType.ShortIronCondor:
                case TradeType.LongIronCondor:
                    contractIds.AddRange(ParseIronCondorContractIds());
                    break;
            }
        }
        return contractIds.ToArray();
    }

    /// <summary>
    /// Parses the iron condor reference into individual option contract ids.
    /// </summary>
    private string[] ParseIronCondorContractIds()
    {
        var contractIds = new List<string>();
        var spreadLegs = Reference.ToUpper().Split(new[] { "X" }, StringSplitOptions.RemoveEmptyEntries);
        if (spreadLegs.Length == 2)
        {
            var putSpreadLeg = spreadLegs.Where(e => e.Contains("P")).SingleOrDefault();
            if (putSpreadLeg != null)
            {
                var putStrikes = putSpreadLeg.Replace("P", "").Split(new[] { ":" }, StringSplitOptions.RemoveEmptyEntries);
                if (putStrikes.Length == 2)
                {
                    contractIds.Add($"{BaseContractSymbol.Trim()}{MaturityDate:yyyyMMdd}P{putStrikes[0].Trim()}");
                    contractIds.Add($"{BaseContractSymbol.Trim()}{MaturityDate:yyyyMMdd}P{putStrikes[1].Trim()}");
                }
            }
            var callSpreadLeg = spreadLegs.Where(e => e.Contains("C")).SingleOrDefault();
            if (callSpreadLeg != null)
            {
                var callStrikes = callSpreadLeg.Replace("C", "").Split(new[] { ":" }, StringSplitOptions.RemoveEmptyEntries);
                if (callStrikes.Length == 2)
                {
                    contractIds.Add($"{BaseContractSymbol.Trim()}{MaturityDate:yyyyMMdd}C{callStrikes[0].Trim()}");
                    contractIds.Add($"{BaseContractSymbol.Trim()}{MaturityDate:yyyyMMdd}C{callStrikes[1].Trim()}");
                }
            }
        }
        return contractIds.ToArray();
    }
}