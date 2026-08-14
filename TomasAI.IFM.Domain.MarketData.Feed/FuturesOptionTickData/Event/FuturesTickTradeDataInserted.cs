using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event.Actor;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event;

/// <summary>
/// Handles durable TickAggregation trade insertions for active futures-option streams.
/// </summary>
public static class FuturesTickTradeDataInserted
{
    static FuturesTickTradeDataInserted() =>
        ServiceId = $"{LogSourceType.FuturesTickTradeDataInserted}";

    static string ServiceId { get; }

    /// <summary>
    /// Combines the exact durable trade with the latest hot-cache option quote and optional Greeks,
    /// then publishes the existing option-trade domain update.
    /// </summary>
    public static async ValueTask<bool> ExecuteAsync(
        this FuturesTickTradeDataInsertedEvent e,
        IEventActorContext context,
        IActorMarketDataFeedEventApi eventApi,
        FuturesOptionTickDataEventParameters p,
        ILogger<FuturesOptionTickDataEventActor> logger)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(eventApi);
        ArgumentNullException.ThrowIfNull(p);
        ArgumentNullException.ThrowIfNull(logger);

        if (e.AssetTypeId != AssetTypeId.FuturesOption
            || !p.Streams.TryGetContract(e.EntityId.ContractId, out var contract)
            || !StringComparer.Ordinal.Equals(contract.ContractId, e.EntityId.ContractId)
            || !p.MarketDataApi.IsTickDataStreamActive(e.EntityId.ContractId))
            return true;

        try
        {
            if (!p.MarketDataApi.TryGetLastOptionTickPrice(
                    e.EntityId.ContractId,
                    out var optionPrice))
                return true;

            var tickData = ToLegacyTickData(e, optionPrice);
            await eventApi.SendOptionTradeTickPriceDataUpdatedEventAsync(
                e,
                tickData).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception)
        {
            await p.StatusConsoleWriter.WriteConsoleAsync(
                LogSourceType.FuturesOptionTickDataEvent,
                5003,
                exception.GetErrorMessage()).ConfigureAwait(false);
            logger.LogErrorEvent(
                ServiceId,
                exception,
                "{EventName} for {ContractId}: option trade update failed",
                nameof(FuturesTickTradeDataInsertedEvent),
                e.EntityId.ContractId);
            throw;
        }
    }

    /// <summary>
    /// Combines the durable trade payload with the hot-cache quote and optional Greeks in the established option tick model.
    /// </summary>
    private static FuturesOptionTickDataV2ReadModel ToLegacyTickData(
        FuturesTickTradeDataInsertedEvent e,
        OptionTickerPriceSnapshot optionPrice)
    {
        var quote = optionPrice.Price.Quote;
        var greeks = optionPrice.Greeks;
        return new FuturesOptionTickDataV2ReadModel(
            e.EntityId.ContractId,
            e.EntityId.ValueDate,
            e.TickDataId.SequenceId,
            ToTimeOnly(e.TradeData.EventTimestampNanoseconds),
            decimal.ToDouble(e.TradeData.Price),
            decimal.ToDouble(quote?.BidPrice ?? 0m),
            decimal.ToDouble(quote?.AskPrice ?? 0m),
            checked((int)(quote?.BidSize ?? 0)),
            checked((int)(quote?.AskSize ?? 0)),
            greeks?.ImpliedVolatility ?? 0d,
            decimal.ToDouble(greeks?.FuturesPrice ?? 0m),
            greeks?.Delta ?? 0d,
            greeks?.Gamma ?? 0d,
            greeks?.Vega ?? 0d,
            greeks?.Theta ?? 0d,
            greeks?.Rho ?? 0d);
    }

    /// <summary>
    /// Converts a Unix nanosecond timestamp to an intraday UTC time, returning midnight for an invalid value.
    /// </summary>
    private static TimeOnly ToTimeOnly(long unixNanoseconds)
    {
        try
        {
            return TimeOnly.FromDateTime(
                DateTimeOffset.UnixEpoch.AddTicks(unixNanoseconds / 100L).UtcDateTime);
        }
        catch (ArgumentOutOfRangeException)
        {
            return TimeOnly.MinValue;
        }
    }
}
