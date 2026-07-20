using System;
using System.Collections.Generic;
using System.Linq;
using MessagePack;
using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.Trade.ViewModels;

/// <summary>
/// MessagePack-serializable view model representing a trade position snapshot on a given value date.
/// </summary>
/// <remarks>
/// Pattern mirrors <see cref="TomasAI.IFM.Shared.MarketDataFeed.ViewModels.FuturesEodClosingPriceReadModel"/>:
/// - Explicit properties with sequential MessagePack keys
/// - A parameterless constructor for serializers
/// - A full constructor marked with <see cref="SerializationConstructorAttribute"/>
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record TradePositionReadModel
{
    /// <summary>Order identifier.</summary>
    [Key(0)]
    public int OrderId { get; init; }

    /// <summary>Trade identifier within the order.</summary>
    [Key(1)]
    public int TradeId { get; init; }

    /// <summary>As-of (value) date for this snapshot.</summary>
    [Key(2)]
    public DateOnly ValueDate { get; init; }

    /// <summary>Trade strategy/type.</summary>
    [Key(3)]
    public TradeType TradeType { get; init; }

    /// <summary>Trade lifecycle status at this snapshot.</summary>
    [Key(4)]
    public TradeStatus TradeStatus { get; init; }

    /// <summary>Remaining days to contract expiry.</summary>
    [Key(5)]
    public int DaysToExpiry { get; init; }

    /// <summary>Total commission for the position.</summary>
    [Key(6)]
    public decimal Commission { get; init; }

    /// <summary>Delta hedge quantity (if any).</summary>
    [Key(7)]
    public int DeltaHedge { get; init; }

    /// <summary>Net spread (pricing metric).</summary>
    [Key(8)]
    public decimal NetSpread { get; init; }

    /// <summary>Current trade value (notional or market value).</summary>
    [Key(9)]
    public decimal TradeValue { get; init; }

    /// <summary>Profit and loss for the position.</summary>
    [Key(10)]
    public decimal TradePnl { get; init; }

    /// <summary>Underlying asset price associated with the position.</summary>
    [Key(11)]
    public decimal AssetPrice { get; init; }

    /// <summary>Out-of-the-money probability.</summary>
    [Key(12)]
    public double OTMProbability { get; init; }

    /// <summary>Forward price used for valuation.</summary>
    [Key(13)]
    public decimal ForwardPrice { get; init; }

    /// <summary>Forward loss ratio.</summary>
    [Key(14)]
    public double ForwardLossRatio { get; init; }

    /// <summary>Probability of loss.</summary>
    [Key(15)]
    public double LossProbability { get; init; }

    /// <summary>Risk-free rate applied in calculations.</summary>
    [Key(16)]
    public double RiskFreeRate { get; init; }

    /// <summary>Creation timestamp (UTC preferred).</summary>
    [Key(17)]
    public DateTime CreatedOn { get; init; }

    /// <summary>Record creator.</summary>
    [Key(18)]
    public string CreatedBy { get; init; } = string.Empty;

    /// <summary>Last update timestamp (UTC preferred).</summary>
    [Key(19)]
    public DateTime UpdatedOn { get; init; }

    /// <summary>Last updater.</summary>
    [Key(20)]
    public string UpdatedBy { get; init; } = string.Empty;

    /// <summary>Attached option legs data for this position.</summary>
    [Key(21)]
    [JsonProperty]
    public OptionTradeLegDataReadModel[] OptionLegData { get; private set; } = Array.Empty<OptionTradeLegDataReadModel>();

    /// <summary>Derived entity identifier (excluded from MessagePack).</summary>
    [JsonIgnore]
    [IgnoreMember]
    public TradePositionEntityId EntityId => new(OrderId, TradeId, ValueDate, TradeType, TradeStatus, DaysToExpiry);

    [JsonIgnore]
    [IgnoreMember]
    public bool IsValid => OrderId > 0 && TradeId > 0 && ValueDate > DateOnly.MinValue;

    /// <summary>
    /// Parameterless constructor for serializers; initializes strings and arrays to safe defaults.
    /// </summary>
    public TradePositionReadModel() { }

    /// <summary>
    /// MessagePack serialization constructor to create a full trade position snapshot.
    /// </summary>
    /// <param name="orderId">Order identifier.</param>
    /// <param name="tradeId">Trade identifier within the order.</param>
    /// <param name="valueDate">As-of (value) date.</param>
    /// <param name="tradeType">Trade strategy/type.</param>
    /// <param name="tradeStatus">Trade lifecycle status.</param>
    /// <param name="daysToExpiry">Remaining days to expiry.</param>
    /// <param name="commission">Total commission.</param>
    /// <param name="deltaHedge">Delta hedge quantity.</param>
    /// <param name="netSpread">Net spread.</param>
    /// <param name="tradeValue">Trade value.</param>
    /// <param name="tradePnl">Profit and loss.</param>
    /// <param name="assetPrice">Underlying asset price.</param>
    /// <param name="otmProbability">Out-of-the-money probability.</param>
    /// <param name="forwardPrice">Forward price used.</param>
    /// <param name="forwardLossRatio">Forward loss ratio.</param>
    /// <param name="lossProbability">Probability of loss.</param>
    /// <param name="riskFreeRate">Risk-free rate.</param>
    /// <param name="createdOn">Creation timestamp.</param>
    /// <param name="createdBy">Record creator.</param>
    /// <param name="updatedOn">Last update timestamp.</param>
    /// <param name="updatedBy">Last updater.</param>
    /// <param name="optionLegData">Attached option legs data.</param>
    [SerializationConstructor]
    public TradePositionReadModel(
        int orderId,
        int tradeId,
        DateOnly valueDate,
        TradeType tradeType,
        TradeStatus tradeStatus,
        int daysToExpiry,
        decimal commission,
        int deltaHedge,
        decimal netSpread,
        decimal tradeValue,
        decimal tradePnl,
        decimal assetPrice,
        double otmProbability,
        decimal forwardPrice,
        double forwardLossRatio,
        double lossProbability,
        double riskFreeRate,
        DateTime createdOn,
        string createdBy,
        DateTime updatedOn,
        string updatedBy,
        OptionTradeLegDataReadModel[]? optionLegData = null)
    {
        OrderId = orderId;
        TradeId = tradeId;
        ValueDate = valueDate;
        TradeType = tradeType;
        TradeStatus = tradeStatus;
        DaysToExpiry = daysToExpiry;
        Commission = commission;
        DeltaHedge = deltaHedge;
        NetSpread = netSpread;
        TradeValue = tradeValue;
        TradePnl = tradePnl;
        AssetPrice = assetPrice;
        OTMProbability = otmProbability;
        ForwardPrice = forwardPrice;
        ForwardLossRatio = forwardLossRatio;
        LossProbability = lossProbability;
        RiskFreeRate = riskFreeRate;
        CreatedOn = createdOn;
        CreatedBy = createdBy ?? string.Empty;
        UpdatedOn = updatedOn;
        UpdatedBy = updatedBy ?? string.Empty;
        OptionLegData = optionLegData ?? [];
    }

    /// <summary>
    /// Factory for a default snapshot with zeroed metrics and empty identifiers.
    /// </summary>
    public static TradePositionReadModel Default(int orderId, int tradeId, TradeType tradeType, DateOnly valueDate, int daysToExpiry, TradeStatus tradeStatus)
        => new (
            orderId: orderId,
            tradeId: tradeId,
            valueDate: valueDate,
            tradeType: tradeType,
            tradeStatus: tradeStatus,
            daysToExpiry: daysToExpiry,
            commission: 0,
            deltaHedge: 0,
            netSpread: 0,
            tradeValue: 0,
            tradePnl: 0,
            assetPrice: 0,
            otmProbability: 0,
            forwardPrice: 0,
            forwardLossRatio: 0,
            lossProbability: 0,
            riskFreeRate: 0,
            createdOn: DateTime.Now,
            createdBy: string.Empty,
            updatedOn: DateTime.Now,
            updatedBy: string.Empty,
            optionLegData: []);

    /// <summary>Creates a deep copy including OptionLegData entries.</summary>
    public TradePositionReadModel Copy()
        => new (
            orderId: EntityId.OrderId,
            tradeId: EntityId.TradeId,
            valueDate: ValueDate,
            tradeType: EntityId.TradeType,
            tradeStatus: TradeStatus,
            daysToExpiry: DaysToExpiry,
            commission: Commission,
            deltaHedge: DeltaHedge,
            netSpread: NetSpread,
            tradeValue: TradeValue,
            tradePnl: TradePnl,
            assetPrice: AssetPrice,
            otmProbability: OTMProbability,
            forwardPrice: ForwardPrice,
            forwardLossRatio: ForwardLossRatio,
            lossProbability: LossProbability,
            riskFreeRate: RiskFreeRate,
            createdOn: CreatedOn,
            createdBy: CreatedBy,
            updatedOn: UpdatedOn,
            updatedBy: UpdatedBy,
            optionLegData: [.. OptionLegData.Select(e => e.Copy())]);

    /// <summary>Replaces current <see cref="OptionLegData"/> with the provided collection.</summary>
    public TradePositionReadModel AddOptionLegData(ICollection<OptionTradeLegDataReadModel> optionLegData)
    {
        OptionLegData = [.. optionLegData];
        return this;
    }

    /// <summary>Updates an existing option leg entry in <see cref="OptionLegData"/> by matching <c>OptionLegId</c>.</summary>
    public TradePositionReadModel SetOptionLegData(OptionTradeLegDataReadModel optionLegData)
    {
        lock (OptionLegData)
        {
            for (var index = 0; index < OptionLegData.Length; index++)
            {
                if (optionLegData.OptionLegId == OptionLegData[index].OptionLegId)
                {
                    OptionLegData[index] = optionLegData;
                    break;
                }
            }
        }
        return this;
    }
}