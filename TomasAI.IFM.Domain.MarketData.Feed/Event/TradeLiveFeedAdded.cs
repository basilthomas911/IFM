using TomasAI.IFM.Domain.MarketData.Feed.Event.Extensions;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.Event;

public static class TradeLiveFeedAdded
{
    static TradeLiveFeedAdded()
    {
        ServiceId = $"{LogSourceType.MarketDataFeedEvent}";
    }

    static string ServiceId { get; } = default!;


    public static async ValueTask<bool> ExecuteAsync(
        this TradeLiveFeedAddedEvent e, IEventActorContext context, MarketDataFeedEventParameters p)
    {
        var source = $"TradeLiveFeedAddedEvent for EntityId: {e.EntityId}";
        try
        {
            // check if option data feed is already active for the trade, turn live feed on
            OptionTradeReadModel optionTrade = await context.GetOptionTradeQueryAsync(e.OrderId, e.TradeId);
            if (optionTrade.IsValid && (optionTrade.OptionLegs?.Length ?? 0) > 0)
            {
                // optionTradeLiveFeedMap will be updated in the event handler of TradeLiveFeedAddedEvent, so we can check if the feed is already active in the map before starting the feed
                if (p.OptionTradeLiveFeedMap.Exists(optionTrade.EntityId))
                {
                    await context.SendTradeLiveFeedAddedFailEventAsync(e, new InvalidOperationException($"Trade Live Feed already active for OrderId: {e.OrderId}, TradeId: {e.TradeId}"));
                    return false;
                }

                var riskFreeRate = await p.BlackboardService.RiskFreeRate.GetAsync(e.EntityId.ValueDate, async (valueDate)
                    => await GetRiskFreeRate(optionTrade.MaturityDate.DayNumber - optionTrade.TradeDate.DayNumber));
                var futuresContract = await context.GetFuturesContractAsync(optionTrade.UnderlyingContractId);
                foreach (var optionLeg in optionTrade.OptionLegs!)
                {
                    var futuresOptionContract = await context.GetFuturesOptionContractAsync(optionLeg.ContractId);
                    var entityId = new FuturesOptionTickEntityId(optionLeg.ContractId, e.EntityId.ValueDate);
                    await context.StartFuturesOptionTickDataStreamingAsync(e.CommandId, entityId, futuresOptionContract, futuresContract, optionTrade.TradeDate, optionTrade.MaturityDate, riskFreeRate);
                    await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, $"Futures Option Tick Data Streaming started for: {entityId.ContractId}");
                }
                p.OptionTradeLiveFeedMap.Add(optionTrade);
                await context.TurnTradeLiveFeedOnAsync(e.CommandId, e.OrderId, e.TradeId, e.EntityId.ValueDate);
                await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, $"Trade Live Feed added for OrderId: {e.OrderId}, TradeId: {e.TradeId}");
                return true;
            }
            else
            {
                await context.SendTradeLiveFeedAddedFailEventAsync(e, new InvalidOperationException($"Trade Live Feed does not exist for OrderId: {e.OrderId}, TradeId: {e.TradeId}"));
                return false;
            }
        }
        catch (Exception ex)
        {
            await context.SendTradeLiveFeedAddedFailEventAsync(e, ex);
            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, -1, ex.GetErrorMessage());
            p.Logger.LogErrorEvent(ServiceId, ex, "{Source}: data feed reset complete failed");
        }
        return false;

        async ValueTask<double> GetRiskFreeRate(int tradingDays)
        {
            var yieldCurveRate = await context.GetLastYieldCurveRateAsync();
            return tradingDays switch
            {
                <= 30 => yieldCurveRate.OneMonth,
                <= 60 => yieldCurveRate.TwoMonth,
                _ => yieldCurveRate.ThreeMonth
            };
        }
    }

}
