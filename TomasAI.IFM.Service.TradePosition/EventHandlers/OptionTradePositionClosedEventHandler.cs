using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventService;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Application.Blackboard;

namespace TomasAI.IFM.Service.TradePosition.EventHandlers;

/// <summary>
/// option trade position closed event handler
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711", Justification = "Existing event service handler identity is used by service registration.")]
public class OptionTradePositionClosedEventHandler : BaseEventServiceHandler,
    IAsyncEventHandler<OptionTradePositionClosedEvent>
{
    readonly IBlackboardService _blackboardService;
    readonly ILogger _logger;

    /// <summary>
    /// option trade position closed event handler constructor
    /// </summary>
    /// <param name="blackboardService"></param>
    /// <param name="statusConsoleWriter"></param>
    /// <param name="logger"></param>
    public OptionTradePositionClosedEventHandler(
        IBlackboardService blackboardService,
        IStatusConsoleWriter statusConsoleWriter,
        ILogger logger)
        : base(statusConsoleWriter)
    {
        _blackboardService = IsArgumentNull.Set(blackboardService);
        _logger = IsArgumentNull.Set(logger);
    }

    /// <summary>
    /// open option trade position
    /// </summary>
    /// <param name="e">option trade position opened event</param>
    /// <returns></returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Event service boundary reports failures through failure events, the status console, and structured exception logs.")]
    public async Task ExecuteAsync(OptionTradePositionClosedEvent e)
    {
        ArgumentNullException.ThrowIfNull(e);
        try
        {
            _blackboardService.Trade.OptionTrade.Remove(e.OptionTradeId);
        }
        catch (Exception ex)
        {
            var errorMessage = $"{e.GetType().Name}: option trade {e.OptionTradeId.OrderId}:{e.OptionTradeId.TradeId} position close failed due to: {ex.GetErrorMessage()}";
            await (WriteConsoleAsync(LogSourceType.TradePosition, OptionTradePositionClosedEvent.ErrorCode, errorMessage)).ConfigureAwait(false);
            TradePositionDiagnosticLog.Failed(_logger, ex, errorMessage);
        }
    }

}
