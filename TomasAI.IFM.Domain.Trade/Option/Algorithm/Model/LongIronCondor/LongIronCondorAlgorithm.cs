using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Application.Blackboard;

namespace TomasAI.IFM.Domain.Trade.Option.Algorithm.Model.LongIronCondor;

public class LongIronCondorAlgorithm : LongIronCondorTradePlan
{
    LongIronCondorRuleEngine _rule;

    public LongIronCondorAlgorithm(TradePlanReadModel e)
       : base(e)
    {
        _rule = new(this);
    }

    public LongIronCondorAlgorithm(DateOnly valueDate, IOptionTradeCollection optionTrades, FuturesEodDataV2ReadModel futuresEodData, FuturesTradeSignalV2ReadModel futuresTradeSignal, IBlackboardService blackboardService)
        : base(valueDate, optionTrades, futuresEodData, futuresTradeSignal, blackboardService)
    {
        _rule = new(this);
    }

    public LongIronCondorRuleEngine Rule => _rule;
}
