using TomasAI.IFM.Domain.MarketData.Feed.Event.Extensions;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.StatusConsole;

namespace TomasAI.IFM.Domain.MarketData.Feed.Event;

public static class TradeLiveFeedRemoved
{
    static TradeLiveFeedRemoved()
    {
        ServiceId = $"{LogSourceType.MarketDataFeedEvent}";
    }

    static string ServiceId { get; } = default!;

    public static async ValueTask<bool> ExecuteAsync(
        this TradeLiveFeedRemovedEvent e, IEventActorContext context, MarketDataFeedEventParameters p)
    {
        var source = $"TradeLiveFeedRemovedEvent for EntityId: {e.EntityId}";
        try
        {
            // check if option data feed is already active for the trade, turn live feed off 
            var optionTrade = await context.GetOptionTradeQueryAsync(e.OrderId, e.TradeId);
            if (optionTrade.IsValid)
            {
                if (!p.OptionTradeLiveFeedMap.Exists(optionTrade.EntityId))
                {
                    await context.SendTradeLiveFeedRemovedFailEventAsync(e, new InvalidOperationException($"Trade Live Feed already inactive for OrderId: {e.OrderId}, TradeId: {e.TradeId}"));
                    return false;
                }
                foreach (var optionLeg in optionTrade.OptionLegs!)
                {
                    var entityId = new FuturesOptionTickEntityId(optionLeg.ContractId, e.EntityId.ValueDate);
                    await context.StopFuturesOptionTickDataStreamingAsync(e.CommandId, entityId, optionLeg.ContractId);
                    await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, $"Futures Option Tick Data Streaming stopped for: {entityId.ContractId}");
                }

                // Stop option data feed if not already active...
                p.OptionTradeLiveFeedMap.Remove(optionTrade);
                await context.TurnTradeLiveFeedOffAsync(e.CommandId, e.OrderId, e.TradeId, e.EntityId.ValueDate);
                await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, $"Trade Live Feed removed for OrderId: {e.OrderId}, TradeId: {e.TradeId}");
                return true;
            }
            else
            {
                await context.SendTradeLiveFeedRemovedFailEventAsync(e, new InvalidOperationException($"Trade Live Feed does not exist for OrderId: {e.OrderId}, TradeId: {e.TradeId}"));
                return false;
            }
        }
        catch (Exception ex)
        {
            await context.SendTradeLiveFeedRemovedFailEventAsync(e, ex);
            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, -1, ex.GetErrorMessage());
            p.Logger.LogErrorEvent(ServiceId, ex, "{Source}: data feed reset complete failed");
        }
        return false;
    }
}
