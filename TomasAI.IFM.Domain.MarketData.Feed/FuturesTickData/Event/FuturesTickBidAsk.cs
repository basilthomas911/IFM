using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Event.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Event;

public static class FuturesTickBidAsk
{
    static FuturesTickBidAsk()
    {
        ServiceId = $"{LogSourceType.FuturesTickDataEvent}";
    }

    static string ServiceId { get; }

    public static async ValueTask<bool> ExecuteAsync(this FuturesTickBidAskEvent e,  IEventActorContext context, FuturesTickDataEventParameters p)
    {
        var source = $"FuturesTickBidAskEvent for EntityId: {e.EntityId}";
        var sp = p.BlackboardService.FuturesTickDataStreamingParameter.Get(e.RequestId);
        if (!sp.IsValid)
            return false;
        try
        {
            // save streamed futures tick data if price has changed...
            var lastFuturesTickData = p.BlackboardService.FuturesTickData.Get(sp.FuturesContract.ContractId, sp.ValueDate);
            if (!lastFuturesTickData.IsValid || lastFuturesTickData.Price != Convert.ToDecimal(e.TickBidAskData.Price) || lastFuturesTickData.TickId != e.TickBidAskData.TickTime)
            {
                var futuresTickData = new FuturesTickDataV2ReadModel(
                    valueDate: sp.ValueDate,
                    contractId: sp.FuturesContract.ContractId,
                    tickTime: TimeOnly.FromDateTime(e.TickBidAskData.TickDate),
                    tickId: e.TickBidAskData.TickTime,
                    price: Convert.ToDecimal(e.TickBidAskData.Price),
                    size: e.TickBidAskData.Size);
                p.BlackboardService.FuturesTickData.Set(sp.FuturesContract.ContractId, sp.ValueDate, futuresTickData);
                await context.InsertFuturesTickDataAsync(sp.FuturesContract, futuresTickData);
                await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesTickDataEvent, $"futures tick price data {sp.FuturesContract.ContractId} price={futuresTickData.Price}");
                p.Logger.LogInformationEvent(ServiceId, "{Source}: futures tick price data {ContractId} price={Price}", source, sp.FuturesContract.ContractId, futuresTickData.Price);
                return true;
            }
        }
        catch (Exception ex)
        {
            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesTickDataEvent, 6004, ex.GetErrorMessage());
            p.Logger.LogErrorEvent(ServiceId, ex, "{Source}: futures tick price data {ContractId} failed", source, sp.FuturesContract.ContractId);
        }
        return false;
    }


}
