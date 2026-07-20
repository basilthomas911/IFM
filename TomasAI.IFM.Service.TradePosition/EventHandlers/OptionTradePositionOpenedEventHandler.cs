using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventService;
using TomasAI.IFM.Shared.Trade.ServiceApi;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Application.Blackboard;

namespace TomasAI.IFM.Service.TradePosition.EventHandlers;

/// <summary>
/// option trade position opened event handler
/// </summary>
public class OptionTradePositionOpenedEventHandler : BaseEventServiceHandler,
    IAsyncEventHandler<OptionTradePositionOpenedEvent>
{
    readonly ITradeQueryApi _tradeQueryApi;
    readonly IBlackboardService _blackboardService;
    readonly ILogger _logger;

    /// <summary>
    /// option trade position opened event handler constructor
    /// </summary>
    /// <param name="blackboardService"></param>
    /// <param name="statusConsoleWriter"></param>
    /// <param name="logger"></param>
    public OptionTradePositionOpenedEventHandler(
        ITradeQueryApi tradeQueryApi,
        IBlackboardService blackboardService,
        IStatusConsoleWriter statusConsoleWriter, 
        ILogger logger) 
        : base(statusConsoleWriter)
    {
        _tradeQueryApi = IsArgumentNull.Set(tradeQueryApi); 
        _blackboardService = IsArgumentNull.Set(blackboardService);
        _logger = IsArgumentNull.Set(logger);
    }

    /// <summary>
    /// open option trade position
    /// </summary>
    /// <param name="e">option trade position opened event</param>
    /// <returns></returns>
    public async Task ExecuteAsync(OptionTradePositionOpenedEvent e)
    {
        try
        {
            var serviceResult = await _tradeQueryApi.GetOptionTradeAsync(e.OptionTradeId.OrderId, e.OptionTradeId.TradeId);
            if (serviceResult.Success)
                _blackboardService.OptionTrade.Set(e.OptionTradeId, serviceResult.Value);
        }
        catch (Exception ex)
        {
            var errorMessage = $"{e.GetType().Name}: option trade {e.OptionTradeId.OrderId}:{e.OptionTradeId.TradeId} position open failed due to: {ex.GetErrorMessage()}";
            await WriteConsoleAsync(LogSourceType.TradePosition, OptionTradePositionOpenedEvent.ErrorCode, errorMessage);
            _logger.LogError(ex, errorMessage);
        }
    }

}
