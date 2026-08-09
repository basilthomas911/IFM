using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.TradeAlgorithm;
using TomasAI.IFM.Domain.Trade.Option.Algorithm.Model;
using TomasAI.IFM.Domain.Trade.Option.Algorithm.Model.LongIronCondor;
using TomasAI.IFM.Domain.Trade.Option.Algorithm.Model.ShortIronCondor;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.TradeAlgorithm.Commands;
using TomasAI.IFM.Domain.Trade.Shared.TradeAlgorithm.Events;

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

    internal async ValueTask<LongIronCondorRuleEngine> GetRuleEngineAsync(ExecuteLongIronCondorAlgorithmCommand e)
        => (await algoBuilder.BuildLongIronCondorAlgorithmAsync(e.ValueDate, e.OptionTrades!, e.FuturesEodData!, e.FuturesTradeSignal!).ConfigureAwait(false)).Rule;

    internal async ValueTask<ShortIronCondorRuleEngine> GetRuleEngineAsync(ExecuteShortIronCondorAlgorithmCommand e)
        => (await algoBuilder.BuildShortIronCondorAlgorithmAsync(e.ValueDate, e.OptionTrades!, e.FuturesEodData!, e.FuturesTradeSignal!).ConfigureAwait(false)).Rule;

    internal bool ApplyAlgorithmExecutedEvent(ICommand<TradeAlgorithmId> e, TradePlanReadModel tradePlan)
        => e switch
        {
            ExecuteLongIronCondorAlgorithmCommand o => Apply(new LongIronCondorAlgorithmExecutedEvent { TradeAlgorithmId = e.EntityId, TradePlan = tradePlan }.RoutedFrom(o), true),
            ExecuteShortIronCondorAlgorithmCommand o => Apply(new ShortIronCondorAlgorithmExecutedEvent { TradeAlgorithmId = e.EntityId, TradePlan = tradePlan }.RoutedFrom(o), true),
            _ => false
        };
}
