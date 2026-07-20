using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Application.Blackboard;

namespace TomasAI.IFM.Domain.Trade.Option.Algorithm.Model.ShortIronCondor;

public class ShortIronCondorAlgorithm : ShortIronCondorTradePlan
{
    ShortIronCondorRuleEngine _rule;

    public ShortIronCondorAlgorithm(TradePlanReadModel e)
       : base(e)
    {
        _rule = new(this);
    }

    public ShortIronCondorAlgorithm(DateOnly valueDate, IOptionTradeCollection optionTrades, FuturesEodDataV2ReadModel futuresEodData, FuturesTradeSignalV2ReadModel futuresTradeSignal, IBlackboardService blackboardService)
        : base(valueDate, optionTrades, futuresEodData, futuresTradeSignal, blackboardService)
    {
        _rule = new(this);
    }

    public ShortIronCondorRuleEngine Rule => _rule;

}
