using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.TradeAlgorithm.Commands;
using TomasAI.IFM.Shared.TradeAlgorithm;
using TomasAI.IFM.Shared.TradeAlgorithm.Events;
using TomasAI.IFM.Domain.Trade.Option.Algorithm.Model;
using TomasAI.IFM.Domain.Trade.Option.Algorithm.Model.LongIronCondor;
using TomasAI.IFM.Domain.Trade.Option.Algorithm.Model.ShortIronCondor;

namespace TomasAI.IFM.Domain.Trade.Option.Algorithm;

public class OptionTradeAlgorithmBoundedContextState(IAlgorithmBuilder algoBuilder)
    : BaseBoundedContextState<OptionTradeAlgorithmBoundedContextState>
{
    TradePlanReadModel? _tradePlan;

    protected override bool Apply(IEvent domainEvent)
    {
        try
        {
            return domainEvent switch
            {
                LongIronCondorAlgorithmExecutedEvent e => On(e),
                ShortIronCondorAlgorithmExecutedEvent e => On(e),
                _ => false
            };
        }
        catch { }
        return false;
    }

    bool On(LongIronCondorAlgorithmExecutedEvent e)
    {
        _tradePlan = e.TradePlan;
        return true;
    }

    bool On(ShortIronCondorAlgorithmExecutedEvent e)
    {
        _tradePlan = e.TradePlan;
        return true;
    }

    /// <summary>
    /// check if we should create new trade plan action if trade plan has changed dramatically
    /// </summary>
    /// <param name="tp"></param>
    /// <returns></returns>
    internal bool HasTradePlanChanged(TradePlanReadModel tp)
        => _tradePlan is null ? true : tp.AssetPrice != _tradePlan.AssetPrice;

    internal LongIronCondorRuleEngine GetRuleEngine(ExecuteLongIronCondorAlgorithmCommand e)
        => algoBuilder.BuildLongIronCondorAlgorithm(e.ValueDate, e.OptionTrades!, e.FuturesEodData!, e.FuturesTradeSignal!).Rule;

    internal ShortIronCondorRuleEngine GetRuleEngine(ExecuteShortIronCondorAlgorithmCommand e)
        => algoBuilder.BuildShortIronCondorAlgorithm(e.ValueDate, e.OptionTrades!, e.FuturesEodData!, e.FuturesTradeSignal!).Rule;

    internal bool ApplyAlgorithmExecutedEvent(ICommand<TradeAlgorithmId> e, TradePlanReadModel tradePlan)
        => e switch
        {
            ExecuteLongIronCondorAlgorithmCommand o => Apply(new LongIronCondorAlgorithmExecutedEvent { TradeAlgorithmId = e.EntityId, TradePlan = tradePlan }.RoutedFrom(o), true),
            ExecuteShortIronCondorAlgorithmCommand o => Apply(new ShortIronCondorAlgorithmExecutedEvent { TradeAlgorithmId = e.EntityId, TradePlan = tradePlan }.RoutedFrom(o), true),
            _ => false
        };
}
