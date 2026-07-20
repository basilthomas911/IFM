using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Model;

public interface IOptionTrade 
{
    OptionTradeEntityId Id { get; }
    int TradeId { get; }
    int OrderId { get; }
    string TradeStrategy { get; }
    DateOnly TradeDate { get; }
    DateOnly MaturityDate { get; }
    TradeType TradeType { get; }
    TradeState TradeState { get; }
    TradeAction TradeAction { get; }
    string UnderlyingContractId { get; }
    AssetType UnderlyingAssetType { get; }
    bool IsPrimaryTrade { get; }
    bool IsHedgeTrade { get; }
    DateTime CreatedOn { get; }
    string CreatedBy { get; }
    DateTime UpdatedOn { get; }
    string UpdatedBy { get; }
    IOptionLegCollection OptionLegs { get; }
    ITradePositionCollection TradePositions { get; }
    ITradeLimit TradeLimit { get; }
    ITradeTypeLimitCollection TradeTypeLimits { get; }
    ITradeFillCollection TradeFills { get; }

    OptionTradeReadModel ToReadModel();
    void AddOptionLeg(IOptionLeg optionLeg);
    void AddOptionLegs(ICollection<IOptionLeg> optionLegs);
    void AddTradePosition(ITradePosition tradePosition);
    void AddTradePositions(ICollection<ITradePosition> tradePosition);
    void AddTradeTypeLimits(ICollection<ITradeTypeLimit> tradeTypeLimits);
    void AddTradeFills(ICollection<ITradeFill> tradeFills, DateTime createdOn, string createdBy);
    decimal GetTradePnl();
    double GetLossProbability();
    double GetShortPutProbability(double assetPrice);
    double GetShortCallProbability(double assetPrice);
    IOptionTrade SetTradeLimit(ITradeLimit tradeLimit);
    IOptionTrade SetTradeState(TradeState tradeState);
}
