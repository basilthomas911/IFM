using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.TradeOrder.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Model.Strategy;

public class IronCondorTrade : OptionTrade
{
    public static IronCondorTrade Create(TradeOrderReadModel e, TradeState tradeState)
        => new (
            orderId: e.OrderId,
            tradeId: e.TradeId,
            tradeStrategy: string.Empty,
            tradeDate: e.TradeDate,
            maturityDate: e.MaturityDate,
            tradeType: e.TradeType,
            tradeState: tradeState,
            tradeAction: e.GetTradeAction(),
            underlyingContractId: e.UnderlyingContractId,
            underlyingAssetType: e.UnderlyingAssetType,
            isPrimaryTrade: e.TradeSubType == TradeSubType.Primary,
            isHedgeTrade: e.TradeSubType == TradeSubType.Hedge,
            createdOn: e.CreatedOn,
            createdBy: e.CreatedBy,
            updatedOn: e.UpdatedOn,
            updatedBy: e.UpdatedBy
     );

    public static IronCondorTrade Create(OptionTradeReadModel e) => new (e);

    public IronCondorTrade(int orderId,
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
        bool isHedgeTrade,
        DateTime createdOn,
        string createdBy,
        DateTime updatedOn,
        string updatedBy):base(orderId, tradeId, tradeStrategy, tradeDate, maturityDate, tradeType, tradeState, tradeAction, underlyingContractId, underlyingAssetType, isPrimaryTrade, isHedgeTrade, createdOn, createdBy, updatedOn, updatedBy)
    {

    }

    public IronCondorTrade(OptionTradeReadModel viewModel)
        :base(viewModel)
    {
    }

    public override decimal GetTradePnl()
    {
        var pcsOpen = TradePositions.Opening(TradeType.PutCreditSpread);
        var ccsOpen = TradePositions.Opening(TradeType.CallCreditSpread);
        var pcsIntraDay = TradePositions.IntraDay(TradeType.PutCreditSpread);
        var ccsIntraDay = TradePositions.IntraDay(TradeType.CallCreditSpread);
        var pcsTradePnl = pcsOpen?.TradeValue - pcsIntraDay?.TradeValue - pcsIntraDay?.Commission;
        var ccsTradePnl = ccsOpen?.TradeValue - ccsIntraDay?.TradeValue - ccsOpen?.Commission;
        return (pcsTradePnl ?? 0m) + (ccsTradePnl ?? 0m);
    }

    public override double GetLossProbability()
    {
        var pcs = TradePositions.IntraDay(TradeType.PutCreditSpread);
        if (pcs is null)
        {
            pcs = TradePositions.EndOfDay(TradeType.PutCreditSpread);
            pcs ??= TradePositions.Opening(TradeType.PutCreditSpread);
        }
        return (pcs is null) ? 0.0 : pcs.LossProbability;
    }
}
