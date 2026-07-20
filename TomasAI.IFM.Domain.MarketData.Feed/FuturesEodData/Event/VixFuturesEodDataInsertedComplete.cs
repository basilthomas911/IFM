using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Event.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Event;

public static class VixFuturesEodDataInsertedComplete
{
    static VixFuturesEodDataInsertedComplete()
    {
        ServiceId = $"{LogSourceType.FuturesEodDataEvent}";
    }
    static string ServiceId { get; } = default!;

    public static async ValueTask<bool> ExecuteAsync(this VixFuturesEodDataInsertedCompleteEvent e, IEventActorContext context, FuturesEodDataEventParameters p)
    {
        var source = $"VixFuturesEodDataInsertedCompleteEvent for EntityId: {e.EntityId}";
        try
        {
            var vixFuturesEodData = await context.GetVixFuturesEodDataAsync(e.VixFuturesTickData.ContractId, e.VixFuturesTickData.ValueDate);
            if (vixFuturesEodData is not null && vixFuturesEodData.Length > 0)
            {
                p.BlackboardService.VixFuturesEodData.Set(e.VixFuturesTickData.ContractId, e.VixFuturesTickData.ValueDate, vixFuturesEodData);
                await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesEodDataEvent, $"{e.VixFuturesTickData.ContractId}:={e.VixFuturesTickData.Price} cached");
                p.Logger.LogInformationEvent(ServiceId, "{Source}: {ContractId}:={Price} cached", source, e.VixFuturesTickData.ContractId, e.VixFuturesTickData.Price);
            }
            return true;
        }
        catch (Exception ex)
        {
            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, 6009, ex.GetErrorMessage());
            p.Logger.LogErrorEvent(ServiceId, ex.GetErrorMessage(), "{Source}: vix futures eod data {ContractId} caching failed", source, e.VixFuturesTickData.ContractId);
        }
        return false;
    }
}
