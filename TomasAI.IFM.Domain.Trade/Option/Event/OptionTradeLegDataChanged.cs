using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Domain.OptionPricer.Shared;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Domain.Trade.Option.Event.Extensions;
using TomasAI.IFM.Domain.OptionPricer.Shared.ServiceApi;
using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Domain.Trade.Option.Event;

/// <summary>Provides the OptionTradeLegDataChanged implementation.</summary>
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
        this OptionTradeLegDataChangedEvent e,  IEventActorContext context, IEventActorContext commandApi, IStatusConsoleWriter statusConsoleWriter, ILogger logger)
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
            await OptionPricerCommandApiExtensions.SubmitSpreadDistributionJobAsync(commandApi, spreadDistributionJob);
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
