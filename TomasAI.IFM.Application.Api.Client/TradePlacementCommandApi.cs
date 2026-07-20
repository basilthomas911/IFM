using TomasAI.IFM.Shared.Application.Commands;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.Commands;
using TomasAI.IFM.Shared.Trade.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Framework.Messaging;

namespace TomasAI.IFM.Application.Api.Client;

/// <summary>
/// trade placement command api constructor
/// </summary>
/// <param name="commandService"></param>
public class TradePlacementCommandApi(ICommandServiceApi commandService) 
    :  ITradePlacementCommandApi
{
    ICommandServiceApi _commandService = IsArgumentNull.Set(commandService);

    /// <summary>
    /// Signals the placement of a futures trade based on the provided trade signal.
    /// </summary>
    /// <remarks>This method asynchronously executes the trade placement command using the provided trade
    /// signal.  Ensure that the <paramref name="futuresTradeSignal"/> contains valid and complete data before calling
    /// this method.</remarks>
    /// <param name="futuresTradeSignal">The trade signal data used to initiate the trade placement. This parameter must not be null.</param>
    /// <returns>A <see cref="ServiceResult{T}"/> containing the unique identifier of the placed trade if the operation succeeds.</returns>
    public async Task<ServiceResult<Guid>> SignalTradePlacementAsync(FuturesTradeSignalV2ReadModel futuresTradeSignal)
        => await new SignalTradePlacementCommand(futuresTradeSignal)
            .ExecuteAsync(e => _commandService.PostCommandAsync(TradePlacementUriPath.Signal, e));

    /// <summary>
    /// Initiates the process of placing a trade and returns the unique identifier for the operation.
    /// </summary>
    /// <remarks>This method sends a command to start the trade placement process. The returned identifier can
    /// be used to track the operation.</remarks>
    /// <param name="tradePlacementId">The identifier of the trade placement to be started.</param>
    /// <returns>A <see cref="ServiceResult{T}"/> containing the unique identifier of the initiated trade placement operation.</returns>
    public async Task<ServiceResult<Guid>> StartTradePlacementAsync(TradePlacementId tradePlacementId)
        => await new StartTradePlacementCommand(tradePlacementId)
            .ExecuteAsync(e => _commandService.PostCommandAsync(TradePlacementUriPath.Signal, e));

    /// <summary>
    /// Stops an ongoing trade placement operation.
    /// </summary>
    /// <remarks>This method sends a command to stop the specified trade placement. Ensure that the <paramref
    /// name="tradePlacementId"/> corresponds to an active trade placement. The returned result indicates the success or
    /// failure of the operation.</remarks>
    /// <param name="tradePlacementId">The unique identifier of the trade placement to stop.</param>
    /// <returns>A <see cref="ServiceResult{T}"/> containing the identifier of the stopped trade placement.</returns>
    public async Task<ServiceResult<Guid>> StopTradePlacementAsync(TradePlacementId tradePlacementId)
        => await new StopTradePlacementCommand(tradePlacementId)
            .ExecuteAsync(e => _commandService.PostCommandAsync(TradePlacementUriPath.Signal, e));

}
