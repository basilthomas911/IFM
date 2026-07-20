using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Plan;

public class TradePlanBoundedContextState 
    : BaseBoundedContextState<TradePlanBoundedContextState>
{
    TradePlanReadModel? _tradePlan;

    /// <summary>
    /// replay all bounded context state events
    /// </summary>
    /// <param name="domainEvent"></param>
    /// <returns></returns>
    protected override bool Apply(IEvent domainEvent)
    {
        try
        {
            return domainEvent switch
            {
                TradePlanUpdatedEvent e => On(e),
                _ => false
            };
        }
        catch { }
        return false;
    }

    /// <summary>
    /// check if we should create new trade plan action if trade plan has changed dramatically
    /// </summary>
    /// <param name="tp"></param>
    /// <returns></returns>
    internal bool HasTradePlanChanged(TradePlanReadModel tp)
        => _tradePlan is null ? true : tp.AssetPrice != _tradePlan.AssetPrice;

    /// <summary>
    /// save updated trade plan and create new trade plan action
    /// </summary>
    /// <param name="e"></param>
    bool On(TradePlanUpdatedEvent e)
    {
        _tradePlan = e.TradePlan;   
        return true;
    }

}
