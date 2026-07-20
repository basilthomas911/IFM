using System;
using System.Collections.Generic;
using System.Text;
using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Shared.Trade.ViewModels;

/// <summary>
/// trade plan action view model
/// </summary>
[MessagePackObject(true)]
public record TradePlanActionReadModel(
    Guid TradePlanId,
    int OrderId,
    int TradeId,
    DateOnly ValueDate,
    DateTime ActionDate,
    ActionType ActionType,
    ActionSubType ActionSubType,
    ActionState ActionState,
    string ActionReason,
    MarketDirectionType MarketTrend,
    MarketVolatilityType MarketVolatility,
    PriceDirectionType MarketDirection,
    PriceVolatilityType VixVolatility,
    TradeRiskType TradeRisk,
    GammaRiskType GammaRisk,
    decimal TradePnl,
    double ForwardLossRatio,
    double MScore,
    decimal NetPrice,
    decimal ForwardPrice,
    double StopLossLimit,
    DateTime CreatedOn,
    string CreatedBy)
{
    [JsonIgnore]
    public TradePlanEntityId Id => new (OrderId, TradeId, ValueDate);

    [JsonIgnore]
    public bool IsValid => OrderId > 0 && TradeId > 0 && ValueDate > DateOnly.MinValue;
}
