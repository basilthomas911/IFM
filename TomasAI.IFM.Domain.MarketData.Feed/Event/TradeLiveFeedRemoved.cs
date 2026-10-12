using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.MarketData.Feed.Event;
public static class TradeLiveFeedRemoved
{
    static TradeLiveFeedRemoved()
    {
        ServiceId = $"{LogSourceType.MarketDataFeedEvent}";
    }

    static string ServiceId { get; }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This actor/service boundary records the exception and contains the failure according to its existing lifecycle or message contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "S3267", Justification = "Perform lifecycle or preparation operations sequentially with cancellation and failure handling at each item; retain explicit ordered awaits.")]
    public static async ValueTask<bool> ExecuteAsync(this TradeLiveFeedRemovedEvent e, IEventActorContext context, IEventActorContext commandApi, IEventActorContext eventApi, MarketDataFeedEventParameters p)
    {
        ArgumentNullException.ThrowIfNull(commandApi);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(eventApi);
        ArgumentNullException.ThrowIfNull(p);
        _ = $"TradeLiveFeedRemovedEvent for EntityId: {e.EntityId}";
        try
        {
            // check if option data feed is already active for the trade, turn live feed off 
            var optionTrade = await (context.GetOptionTradeQueryAsync(e.OrderId, e.TradeId)).ConfigureAwait(false);
            if (optionTrade.IsValid)
            {
                if (!p.OptionTradeLiveFeedMap.Exists(optionTrade.EntityId))
                {
                    await (eventApi.SendTradeLiveFeedRemovedFailEventAsync(e, new InvalidOperationException($"Trade Live Feed already inactive for OrderId: {e.OrderId}, TradeId: {e.TradeId}"))).ConfigureAwait(false);
                    return false;
                }

                foreach (var optionLeg in optionTrade.OptionLegs!)
                {
                    var entityId = new FuturesOptionTickEntityId(optionLeg.ContractId, e.EntityId.ValueDate);
                    await (commandApi.StopFuturesOptionTickDataStreamingAsync(e.CommandId, entityId, optionLeg.ContractId)).ConfigureAwait(false);
                    await (p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, $"Futures Option Tick Data Streaming stopped for: {entityId.ContractId}")).ConfigureAwait(false);
                }

                // Stop option data feed if not already active...
                p.OptionTradeLiveFeedMap.Remove(optionTrade);
                await (commandApi.TurnTradeLiveFeedOffAsync(e.CommandId, TradeEntityId.Parse(e.Subject.EntityId), e.EntityId.ValueDate)).ConfigureAwait(false);
                await (p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, $"Trade Live Feed removed for OrderId: {e.OrderId}, TradeId: {e.TradeId}")).ConfigureAwait(false);
                return true;
            }
            else
            {
                await (eventApi.SendTradeLiveFeedRemovedFailEventAsync(e, new InvalidOperationException($"Trade Live Feed does not exist for OrderId: {e.OrderId}, TradeId: {e.TradeId}"))).ConfigureAwait(false);
                return false;
            }
        }
        catch (Exception ex)
        {
            await (eventApi.SendTradeLiveFeedRemovedFailEventAsync(e, ex)).ConfigureAwait(false);
            await (p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, -1, ex.GetErrorMessage())).ConfigureAwait(false);
            p.Logger.LogErrorEvent(ServiceId, ex, "{Source}: data feed reset complete failed");
        }

        return false;
    }
}
