using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.OptionPricer.Shared;
using TomasAI.IFM.Domain.OptionPricer.Shared.ServiceApi;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Futures.Option.Event.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Event;

public static class OptionTradeLegDataChanged
{
    static readonly string ServiceId = $"{LogSourceType.OptionTradeEvent}";

    /// <summary>Submits the compatibility spread-distribution job for an updated option leg.</summary>
    public static async ValueTask ExecuteAsync(
        this OptionTradeLegDataChangedEvent source,
        IFuturesOptionTradeEventContext context)
    {
        var operation = $"OptionTradeLegDataChangedEvent for EntityId: {source.EntityId}";
        try
        {
            var job = new SpreadDistributionJobReadModel(
                orderId: source.Key.OrderId,
                tradeId: source.Key.TradeId,
                tradeType: source.Key.TradeType,
                tradeStatus: source.Key.TradeStatus,
                valueDate: source.Key.ValueDate,
                daysToExpiry: source.Key.DaysToExpiry,
                jobSubmitted: DateTime.UtcNow,
                jobStatus: SpreadDistributionJobStatus.InProgress,
                jobCompleted: null,
                jobFailed: null,
                inProgress: true,
                lossProbabilityFactor: 0.1);
            _ = await OptionPricerCommandApiExtensions
                .SubmitSpreadDistributionJobAsync(context, job).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await context.StatusConsoleWriter.WriteConsoleAsync(
                LogSourceType.OptionTradeEvent,
                OptionTradeLegDataChangedEvent.ErrorCode,
                exception.GetErrorMessage()).ConfigureAwait(false);
            context.Logger.LogErrorEvent(
                ServiceId,
                exception,
                "{Operation}: option trade leg data change failed",
                operation);
        }
    }
}
