using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Domain.Trade.Option.Event.Extensions;
using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Domain.Trade.Option.Event;

public static class OptionTradeLegDataChanged
{
    static OptionTradeLegDataChanged()
    {
        ServiceId = $"{LogSourceType.OptionTradeEvent}";
    }
    static string ServiceId { get; }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="e"></param>
    /// <param name="context"></param>
    /// <param name="statusConsoleWriter"></param>
    /// <param name="logger"></param>
    /// <returns></returns>
    public static async ValueTask<bool> ExecuteAsync(
        this OptionTradeLegDataChangedEvent e,  IEventActorContext context, IStatusConsoleWriter statusConsoleWriter, ILogger logger)
    {
        var source = $"OptionTradeLegDataChangedEvent for EntityId: {e.EntityId}";
        try
        {
            var spreadDistributionJob = new SpreadDistributionJobReadModel(
                   orderId: e.Key.OrderId,
                   tradeId: e.Key.TradeId,
                   tradeType: e.Key.TradeType,
                   tradeStatus: e.Key.TradeStatus,
                   valueDate: e.Key.ValueDate,
                   daysToExpiry: e.Key.DaysToExpiry,
                   jobSubmitted: DateTime.UtcNow,
                   jobStatus: SpreadDistributionJobStatus.InProgress,
                   jobCompleted: null,
                   jobFailed: null,
                   inProgress: true,
                   lossProbabilityFactor: 0.1);
            await context.SubmitSpreadDistributionJobAsync(spreadDistributionJob);
            return true;
        }
        catch (Exception ex)
        {
            await statusConsoleWriter.WriteConsoleAsync(LogSourceType.OptionTradeEvent, OptionTradeLegDataChangedEvent.ErrorCode, ex.GetErrorMessage());
            logger.LogErrorEvent(ServiceId, ex, "{Source}: option trade leg data change failed", source);
        }
        return false;
    }
}
