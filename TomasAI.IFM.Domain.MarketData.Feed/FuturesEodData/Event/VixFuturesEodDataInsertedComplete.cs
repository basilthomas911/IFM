using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Event.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Event.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Event;

/// <summary>Handles the VixFuturesEodDataInsertedCompleteEvent message in the FuturesEodDataEventActor lifecycle.</summary>
public static class VixFuturesEodDataInsertedComplete
{
    static VixFuturesEodDataInsertedComplete()
    {
        ServiceId = $"{LogSourceType.FuturesEodDataEvent}";
    }
    static string ServiceId { get; } 
    /// <summary>Caches a completed VIX futures EOD observation and reports the outcome.</summary>
    public static ValueTask<bool> ExecuteAsync(this VixFuturesEodDataInsertedCompleteEvent e,
        IEventActorContext context, FuturesEodDataEventParameters p, ILogger<FuturesEodDataEventActor> logger)
        
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(p);
        return ExecuteCoreAsync(e, context, p, logger);
    }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This actor/service boundary records the exception and contains the failure according to its existing lifecycle or message contract.")] 
    /// <summary>Runs the shared VX cache behavior for Event and Realtime ownership.</summary>
    internal static async ValueTask<bool> ExecuteCoreAsync(VixFuturesEodDataInsertedCompleteEvent e,
        IEventActorContext context, FuturesEodDataEventParameters p, ILogger logger)
    {
        var source = $"VixFuturesEodDataInsertedCompleteEvent for EntityId: {e.EntityId}";
        try
        {
            var vixFuturesEodData = await (context.GetVixFuturesEodDataAsync(e.VixFuturesTickData.ContractId, e.VixFuturesTickData.ValueDate)).ConfigureAwait(false);
            if (vixFuturesEodData is not null && vixFuturesEodData.Length > 0)
            {
                p.BlackboardService.MarketDataFeed.VixFuturesEodData.Set(e.VixFuturesTickData.ContractId, e.VixFuturesTickData.ValueDate, vixFuturesEodData);
                await (p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesEodDataEvent, $"{e.VixFuturesTickData.ContractId}:={e.VixFuturesTickData.Price} cached")).ConfigureAwait(false);
                logger.LogInformationEvent(ServiceId, "{Source}: {ContractId}:={Price} cached", source, e.VixFuturesTickData.ContractId, e.VixFuturesTickData.Price);
            }
            return true;
        }
        catch (Exception ex)
        {
            logger.LogErrorEvent(ServiceId, ex, "{Source}: vix futures eod data {ContractId} caching failed", source, e.VixFuturesTickData.ContractId);
            await (p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, 6009, ex.GetErrorMessage())).ConfigureAwait(false);
        }
        return false;
    }
}
