using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Domain.Trade.Model;

public abstract class AbstractTrade(
    int orderId,
    int tradeId,
    string tradeStrategy,
    DateOnly tradeDate,
    DateOnly maturityDate,
    TradeType tradeType,
    TradeState tradeState,
    TradeAction tradeAction,
    string underlyingContractId,
    AssetType underlyingAssetType,
    bool isPrimaryTrade,
    bool isHedgeTrade)
{
    public int OrderId { get; private set; } = orderId;
    public int TradeId { get; private set; } = tradeId;
    public string TradeStrategy { get; private set; } = tradeStrategy;
    public DateOnly TradeDate { get; private set; } = tradeDate;
    public DateOnly MaturityDate { get; private set; } = maturityDate;
    public TradeType TradeType { get; private set; } = tradeType;
    public TradeState TradeState { get; private set; } = tradeState;
    public TradeAction TradeAction { get; private set; } = tradeAction;
    public string UnderlyingContractId { get; private set; } = (underlyingContractId ?? string.Empty).Replace("\r\n", "").Trim();
    public AssetType UnderlyingAssetType { get; private set; } = underlyingAssetType;
    public bool IsPrimaryTrade { get; private set; } = isPrimaryTrade;
    public bool IsHedgeTrade { get; private set; } = isHedgeTrade;

    protected void SetTradeState(TradeState tradeState)
        => TradeState = tradeState;
}
