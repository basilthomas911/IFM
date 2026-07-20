using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.Commands;
using TomasAI.IFM.Shared.Trade.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Framework.Messaging;

namespace TomasAI.IFM.Application.Command.Client;

/// <summary>
/// trade placement command api constructor
/// </summary>
/// <param name="commandService"></param>
public class TradePlacementCommandApi(ICommandService commandService) :  ITradePlacementCommandApi
{
    const string TradePlacementController = "TradePlacement";
    ICommandService _commandService = IsArgumentNull.Set(commandService);

    /// <summary>
    /// signal trade placement with current futures trade signal
    /// </summary>
    /// <param name="tradePlan"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> SignalTradePlacementAsync(FuturesTradeSignalV2ReadModel futuresTradeSignal)
        => await new SignalTradePlacementCommand(futuresTradeSignal)
            .ExecuteAsync(e => _commandService.PostApiCommandAsync(e, TradePlacementController));

    /// <summary>
    /// start trade placement signaler
    /// </summary>
    /// <param name="tradePlacementId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> StartTradePlacementAsync(TradePlacementId tradePlacementId)
        => await new StartTradePlacementCommand(tradePlacementId)
            .ExecuteAsync(e => _commandService.PostApiCommandAsync(e, TradePlacementController));

    /// <summary>
    /// stop trade placement signaler
    /// </summary>
    /// <param name="tradePlacementId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> StopTradePlacementAsync(TradePlacementId tradePlacementId)
        => await new StopTradePlacementCommand(tradePlacementId)
            .ExecuteAsync(e => _commandService.PostApiCommandAsync(e, TradePlacementController));

}
