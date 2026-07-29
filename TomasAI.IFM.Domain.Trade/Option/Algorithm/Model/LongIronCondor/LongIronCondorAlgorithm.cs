using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

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
