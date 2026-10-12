using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.MarketData.Feed.Event;
public static class TradeLiveFeedAdded
{
    static TradeLiveFeedAdded()
    {
        ServiceId = $"{LogSourceType.MarketDataFeedEvent}";
    }

    static string ServiceId { get; }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This actor/service boundary records the exception and contains the failure according to its existing lifecycle or message contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "S3267", Justification = "Perform lifecycle or preparation operations sequentially with cancellation and failure handling at each item; retain explicit ordered awaits.")]
    public static async ValueTask<bool> ExecuteAsync(this TradeLiveFeedAddedEvent e, IEventActorContext context, IEventActorContext commandApi, IEventActorContext eventApi, MarketDataFeedEventParameters p)
    {
        ArgumentNullException.ThrowIfNull(commandApi);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(eventApi);
        ArgumentNullException.ThrowIfNull(p);
        _ = $"TradeLiveFeedAddedEvent for EntityId: {e.EntityId}";
        try
        {
            // check if option data feed is already active for the trade, turn live feed on
            OptionTradeReadModel optionTrade = await (context.GetOptionTradeQueryAsync(e.OrderId, e.TradeId)).ConfigureAwait(false);
            if (optionTrade.IsValid && (optionTrade.OptionLegs?.Length ?? 0) > 0)
            {
                // optionTradeLiveFeedMap will be updated in the event handler of TradeLiveFeedAddedEvent, so we can check if the feed is already active in the map before starting the feed
                if (p.OptionTradeLiveFeedMap.Exists(optionTrade.EntityId))
                {
                    await (eventApi.SendTradeLiveFeedAddedFailEventAsync(e, new InvalidOperationException($"Trade Live Feed already active for OrderId: {e.OrderId}, TradeId: {e.TradeId}"))).ConfigureAwait(false);
                    return false;
                }

                var riskFreeRate = await (p.BlackboardService.MarketData.RiskFreeRate.GetAsync(e.EntityId.ValueDate, async (valueDate) => await (GetRiskFreeRateAsync(context, optionTrade.MaturityDate.DayNumber - optionTrade.TradeDate.DayNumber)).ConfigureAwait(false))).ConfigureAwait(false);
                var futuresContract = await (context.GetFuturesContractAsync(optionTrade.UnderlyingContractId)).ConfigureAwait(false);
                foreach (var optionLeg in optionTrade.OptionLegs!)
                {
                    var futuresOptionContract = await (context.GetFuturesOptionContractAsync(optionLeg.ContractId)).ConfigureAwait(false);
                    var entityId = new FuturesOptionTickEntityId(optionLeg.ContractId, e.EntityId.ValueDate);
                    await (commandApi.StartFuturesOptionTickDataStreamingAsync(e.CommandId, entityId, futuresOptionContract, futuresContract, optionTrade.TradeDate, optionTrade.MaturityDate, riskFreeRate)).ConfigureAwait(false);
                    await (p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, $"Futures Option Tick Data Streaming started for: {entityId.ContractId}")).ConfigureAwait(false);
                }

                p.OptionTradeLiveFeedMap.Add(optionTrade);
                await (commandApi.TurnTradeLiveFeedOnAsync(e.CommandId, TradeEntityId.Parse(e.Subject.EntityId), e.EntityId.ValueDate)).ConfigureAwait(false);
                await (p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, $"Trade Live Feed added for OrderId: {e.OrderId}, TradeId: {e.TradeId}")).ConfigureAwait(false);
                return true;
            }
            else
            {
                await (eventApi.SendTradeLiveFeedAddedFailEventAsync(e, new InvalidOperationException($"Trade Live Feed does not exist for OrderId: {e.OrderId}, TradeId: {e.TradeId}"))).ConfigureAwait(false);
                return false;
            }
        }
        catch (Exception ex)
        {
            await (eventApi.SendTradeLiveFeedAddedFailEventAsync(e, ex)).ConfigureAwait(false);
            await (p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, -1, ex.GetErrorMessage())).ConfigureAwait(false);
            p.Logger.LogErrorEvent(ServiceId, ex, "{Source}: data feed reset complete failed");
        }

        return false;
    }

    /// <summary>Selects the one-, two-, or three-month yield rate for the trade duration.</summary>
    static async ValueTask<double> GetRiskFreeRateAsync(IEventActorContext context, int tradingDays)
    {
        var yieldCurveRate = await (context.GetLastYieldCurveRateAsync()).ConfigureAwait(false);
        return tradingDays switch
        {
            <= 30 => yieldCurveRate.OneMonth,
            <= 60 => yieldCurveRate.TwoMonth,
            _ => yieldCurveRate.ThreeMonth
        };
    }
}
