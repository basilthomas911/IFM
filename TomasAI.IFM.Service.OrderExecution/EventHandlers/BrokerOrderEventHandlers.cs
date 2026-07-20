using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventService;
using TomasAI.IFM.Shared.Trade.ServiceApi;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Shared.TradeOrder.Events;

namespace TomasAI.IFM.Service.OrderExecution.EventHandlers;

public class BrokerOrderEventHandlers : BaseEventServiceHandler,
    IAsyncEventHandler<BrokerOrderOpenedEvent, OrderExecutionService>,
    IAsyncEventHandler<BrokerOrderFilledEvent, OrderExecutionService>,
    IAsyncEventHandler<BrokerOrderClosedEvent, OrderExecutionService>
{
    readonly ITradeOrderCommandApi _tradeOrderCommandApi;
    readonly ILogger<BrokerOrderEventHandlers> _logger;

    public BrokerOrderEventHandlers(
        ITradeOrderCommandApi tradeOrderCommandApi,
        IStatusConsoleWriter statusConsoleWriter, 
        ILogger<BrokerOrderEventHandlers> logger) : base(statusConsoleWriter)
    {
        _tradeOrderCommandApi = tradeOrderCommandApi ?? throw new ArgumentNullException(nameof(tradeOrderCommandApi));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// handle broker order opened event
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task ExecuteAsync(BrokerOrderOpenedEvent e)
    {
        try
        {
            await _tradeOrderCommandApi.OpenTradeOrderAsync(e.TradeOrderId, e.Executed, e.ErrorMessage);
            if (!e.Executed)
                await _tradeOrderCommandApi.CancelTradeOrderAsync(e.TradeOrderId);
        }
        catch (Exception ex)
        {
            await WriteConsoleAsync(LogSourceType.OrderExecution, 6010, ex.GetErrorMessage());
            _logger.LogError(ex.GetErrorMessage(), $"{LogSourceType.OrderExecution}: broker order {e.TradeOrderId.OrderId}:{e.TradeOrderId.TradeId} submit failed");
        }
    }

    /// <summary>
    /// handle broker order filled event
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task ExecuteAsync(BrokerOrderFilledEvent e)
    {
        try
        {
            await _tradeOrderCommandApi.FillTradeOrderAsync(e.TradeOrderId, e.TradeFill, e.Executed, e.ErrorMessage);
            if (!e.Executed)
                await _tradeOrderCommandApi.CancelTradeOrderAsync(e.TradeOrderId);
        }
        catch (Exception ex)
        {
            await WriteConsoleAsync(LogSourceType.OrderExecution, 6010, ex.GetErrorMessage());
            _logger.LogError(ex.GetErrorMessage(), $"{LogSourceType.OrderExecution}: broker order {e.TradeOrderId.OrderId}:{e.TradeOrderId.TradeId} fill failed");
        }
    }

    /// <summary>
    /// handle broker order closed event
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task ExecuteAsync(BrokerOrderClosedEvent e)
    {
        try
        {
            await _tradeOrderCommandApi.CloseTradeOrderAsync(e.TradeOrderId, e.Executed, e.ErrorMessage);
            if (!e.Executed)
                await _tradeOrderCommandApi.CancelTradeOrderAsync(e.TradeOrderId);
        }
        catch (Exception ex)
        {
            await WriteConsoleAsync(LogSourceType.OrderExecution, 6010, ex.GetErrorMessage());
            _logger.LogError(ex.GetErrorMessage(), $"{LogSourceType.OrderExecution}: broker order {e.TradeOrderId.OrderId}:{e.TradeOrderId.TradeId} close failed");
        }
    }

}
