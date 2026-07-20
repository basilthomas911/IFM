using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventService;
using TomasAI.IFM.Shared.Trade.ServiceApi;
using TomasAI.IFM.Shared.TradeOrder.Events;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Shared.StatusConsole;

namespace TomasAI.IFM.Service.OrderExecution.EventHandlers;

public class TradeOrderEventeEventHandlers : BaseEventServiceHandler,
    IAsyncEventHandler<TradeOrderPlacedEvent, OrderExecutionService>
{
    readonly ITradeCommandApi _tradeCommandApi;
    readonly ILogger<TradeOrderEventeEventHandlers> _logger;

    public TradeOrderEventeEventHandlers(
        ITradeCommandApi tradeCommandApi,
        IStatusConsoleWriter statusConsoleWriter, 
        ILogger<TradeOrderEventeEventHandlers> logger) : base(statusConsoleWriter)
    {
        _tradeCommandApi = IsArgumentNull.Set(tradeCommandApi);
        _logger = IsArgumentNull.Set(logger);
    }

    /// <summary>
    /// on trade order submitted
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task ExecuteAsync(TradeOrderPlacedEvent e)
    {
        try
        {
        }
        catch (Exception ex)
        {
            await WriteConsoleAsync(LogSourceType.OrderExecution, 6010, ex.GetErrorMessage());
            _logger.LogError(ex, $"{LogSourceType.OrderExecution}: trade order {e.TradeOrder.OrderId}:{e.TradeOrder.TradeId} submit failed");
        }
    }

}
