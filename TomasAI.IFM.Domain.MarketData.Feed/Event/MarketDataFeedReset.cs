using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.Event.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Extensions;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Domain.MarketData.Feed.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.Event;

/// <summary>Handles the MarketDataFeedResetEvent message in the MarketDataFeedEventActor lifecycle.</summary>
public static class MarketDataFeedReset
{
    static MarketDataFeedReset()
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
        this MarketDataFeedResetEvent e,
        IEventActorContext context,
        IEventActorContext eventApi,
        MarketDataFeedEventParameters p, ILogger<MarketDataFeedEventActor> logger)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(eventApi);
        ArgumentNullException.ThrowIfNull(p);
        var source = $"MarketDataFeedResetEvent for EntityId: {e.EntityId}";
        try
        {
            await (p.MarketDataLifecycle.ResetAsync(e.ValueDate, e.CommandId)).ConfigureAwait(false);
            await (eventApi.MarketDataFeedResetCompleteAsync(e)).ConfigureAwait(false);
            await (p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, "Market data feed reset")).ConfigureAwait(false);
            logger.LogInformationEvent(ServiceId, "{Source}: market data feed reset", source);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogErrorEvent(ServiceId, ex, "{Source}: market data feed reset failed", source);
            await (eventApi.MarketDataFeedResetFailAsync(e, ex)).ConfigureAwait(false);
            await (p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, MarketDataFeedResetEvent.ErrorCode, ex.GetErrorMessage())).ConfigureAwait(false);
        }
        return false;
    }

}
