using TomasAI.IFM.Domain.MarketData.Feed.Event.Actor;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Extensions;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.Event;

/// <summary>Handles the MarketDataFeedStartedEvent message in the MarketDataFeedEventActor lifecycle.</summary>
public static class MarketDataFeedStarted
{
    static MarketDataFeedStarted()
    {
        ServiceId = $"{LogSourceType.MarketDataFeedEvent}";
    }

    static string ServiceId { get; } 
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This actor/service boundary records the exception and contains the failure according to its existing lifecycle or message contract.")]     /// <summary>
    /// 
    /// </summary>
    /// <param name="e"></param>
    /// <param name="p"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static async ValueTask<bool> ExecuteAsync(
        this MarketDataFeedStartedEvent e,
        IEventActorContext context,
        IEventActorContext eventApi,
        MarketDataFeedEventParameters p, ILogger<MarketDataFeedEventActor> logger)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(eventApi);
        ArgumentNullException.ThrowIfNull(p);
        var source = $"MarketDataFeedStartedEvent for EntityId: {e.EntityId}";
        try
        {
            await (p.MarketDataLifecycle.StartAsync(
                e.ValueDate,
                (_, errorCode, errorMsg) => p.StatusConsoleWriter.WriteConsoleAsync(
                    LogSourceType.MarketDataFeedEvent, errorCode, errorMsg))).ConfigureAwait(false);
            await (eventApi.SendMarketDataFeedStartedCompleteAsync(e)).ConfigureAwait(false);
            await (p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, "Market data feed started")).ConfigureAwait(false);
            logger.LogInformationEvent(ServiceId, "{Source}: market data feed started", source);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogErrorEvent(ServiceId, ex, "{Source}: market data feed start failed", source);
            await (eventApi.SendMarketDataFeedStartedFailAsync(e, ex)).ConfigureAwait(false);
            await (p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, MarketDataFeedStartedEvent.ErrorCode, ex.GetErrorMessage())).ConfigureAwait(false);
        }
        return false;
    }

}
