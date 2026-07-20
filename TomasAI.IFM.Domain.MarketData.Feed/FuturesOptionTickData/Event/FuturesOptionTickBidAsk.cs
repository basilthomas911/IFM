using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event;

public static class FuturesOptionTickBidAsk
{
    static FuturesOptionTickBidAsk()
    {
        ServiceId = $"{LogSourceType.FuturesOptionTickDataEvent}";
    }

    static string ServiceId { get; }

    public static async ValueTask<bool> ExecuteAsync(this FuturesOptionTickBidAskEvent e,  IEventActorContext context, FuturesOptionTickDataEventParameters p)
    {
        var source = $"FuturesOptionTickBidAskEvent for EntityId: {e.EntityId}";

        // Resolve the streaming request context using the event's request identifier.
        // If no matching streaming request exists, exit early — the event cannot be processed.
        var streamingRequestId = p.BlackboardService.StreamingRequestId.Get(e.RequestId);
        if (!streamingRequestId.IsValid)
            return false;
        try
        {
            // Retrieve the latest underlying futures tick data for the contract and value date
            // associated with this streaming request. The tick data must be valid with a positive
            // price to proceed with option calculations.
            var futuresTickData = p.BlackboardService.FuturesTickData.Get(streamingRequestId.UnderlyingContract.ContractId, streamingRequestId.ValueDate);
            if (futuresTickData.IsValid && futuresTickData.Price > 0m)
            {
                // Compute the full option tick data (including greeks) from the raw bid/ask
                // tick event and the current underlying futures price. The local function
                // GetFuturesOptionTickData uses the OptionCalculator to derive implied
                // volatility, delta, gamma, vega, theta, and rho.
                var futuresOptionTickData = GetFuturesOptionTickData(e.TickBidAskData, Convert.ToDouble(futuresTickData.Price));
                if (futuresOptionTickData.IsValid)
                {
                    // Persist the computed option tick data via the insert command.
                    await context.InsertFuturesOptionTickDataAsync(streamingRequestId.UnderlyingContract, futuresOptionTickData);

                    // Compare the new bid/ask prices against the last recorded tick price data
                    // to determine whether a meaningful price change has occurred.
                    var lastFuturesOptionTickPriceData = p.BlackboardService.FuturesOptionTickPriceData.Get(futuresOptionTickData.ContractId, streamingRequestId.ValueDate);
                    if (!lastFuturesOptionTickPriceData.IsValid
                        || Convert.ToDecimal(lastFuturesOptionTickPriceData.BidPrice) != Convert.ToDecimal(futuresOptionTickData.BidPrice)
                        || Convert.ToDecimal(lastFuturesOptionTickPriceData.AskPrice) != Convert.ToDecimal(futuresOptionTickData.AskPrice))
                    {
                        // Price has changed — retrieve the current risk-free rate and the set
                        // of live option trades linked to this contract for leg-data updates.
                        var riskFreeRate = p.BlackboardService.RiskFreeRate.Get(streamingRequestId.ValueDate);
                        var optionTrades = p.OptionTradeLiveFeedMap[streamingRequestId.OptionContract.ContractId];

                        // Propagate the updated tick data to all matching option trade legs
                        await context.UpdateFuturesOptionTradeLegDataAsync(futuresOptionTickData, riskFreeRate, optionTrades);

                        // Persist the new tick price data and update the in-memory blackboard
                        // state so subsequent comparisons use the latest values.
                        await context.InsertFuturesOptionTickPriceDataAsync(streamingRequestId.UnderlyingContract, futuresOptionTickData);
                        p.BlackboardService.FuturesOptionTickPriceData.Set(futuresOptionTickData.ContractId, streamingRequestId.ValueDate, futuresOptionTickData);
                        p.Logger.LogInformationEvent(ServiceId, "{Source}: futures option tick {ContractId} price: {OptionPrice:F2}", source, streamingRequestId.OptionContract.ContractId, futuresOptionTickData.OptionPrice);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesOptionTickDataEvent, 6007, ex.GetErrorMessage());
            p.Logger.LogErrorEvent(ServiceId, ex, "{Source  }: futures option {sp.FuturesOptionContract.ContractId} streaming failed", source, streamingRequestId.OptionContract.ContractId);
        }
        return false;

        // Local function: computes a full FuturesOptionTickDataV2ReadModel from raw bid/ask
        // tick data and the current underlying futures price. Uses OptionCalculator to derive
        // option greeks (IV, delta, gamma, vega, theta, rho) based on the option mid-price,
        // strike price, risk-free rate, and days to expiry. Returns Default if greek
        // calculation fails.
        FuturesOptionTickDataV2ReadModel GetFuturesOptionTickData(FuturesOptionTickBidAskReadModel o, double futuresPrice)
        {
            var daysToExpiry = streamingRequestId.MaturityDate.DayNumber - streamingRequestId.ValueDate.DayNumber;
            var optionCalculator = new OptionCalculator(streamingRequestId.ValueDate, streamingRequestId.MaturityDate);
            var optionValue = (o.BidPrice + o.AskPrice) / 2;
            var optionGreeks = optionCalculator.GetOptionGreeks(streamingRequestId.OptionContract.OptionType, futuresPrice, streamingRequestId.OptionContract.StrikePrice, optionValue, streamingRequestId.RiskFreeRate);
            if (!optionGreeks.Success)
                return FuturesOptionTickDataV2ReadModel.Default;
            var timeValue = daysToExpiry / 365.0;
            return new FuturesOptionTickDataV2ReadModel(
                streamingRequestId.OptionContract.ContractId,
                streamingRequestId.ValueDate,
                o.TickTime,
                TimeOnly.FromDateTime(o.TickDate),
                optionValue,
                o.BidPrice,
                o.AskPrice,
                o.BidSize,
                o.AskSize,
                optionGreeks.ImpliedVolatility,
                futuresPrice,
                optionGreeks.Delta,
                optionGreeks.Gamma,
                optionGreeks.Vega,
                optionGreeks.Theta,
                optionGreeks.Rho);
        }
    }
}
