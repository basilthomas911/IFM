using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Realtime;

/// <summary>Builds the external option-price notification from one live trade and the hot quote cache.</summary>
internal static class FuturesTickTradeDataInserted
{
    static readonly string ServiceId = $"{LogSourceType.FuturesTickTradeDataInserted}";

    internal static async ValueTask<bool> ExecuteAsync(
        this FuturesTickTradeDataInsertedEvent source,
        IActorMarketDataFeedEventApi eventApi,
        IMarketDataApi marketDataApi,
        IStatusConsoleWriter statusConsoleWriter,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(eventApi);
        ArgumentNullException.ThrowIfNull(marketDataApi);
        ArgumentNullException.ThrowIfNull(statusConsoleWriter);
        ArgumentNullException.ThrowIfNull(logger);

        if (source.AssetTypeId != AssetTypeId.FuturesOption
            || !marketDataApi.IsTickDataStreamActive(source.EntityId.ContractId))
            return true;

        try
        {
            var contract = await marketDataApi.GetFuturesOptionContractAsync(
                    source.EntityId.ContractId)
                .ConfigureAwait(false);
            if (contract is null
                || !marketDataApi.TryGetLastOptionTickPrice(
                    source.EntityId.ContractId,
                    out var optionPrice))
                return true;

            await eventApi.SendOptionTradeTickPriceDataUpdatedEventAsync(
                    source,
                    ToOptionTickData(source, optionPrice))
                .ConfigureAwait(false);
            return true;
        }
        catch (Exception exception)
        {
            await statusConsoleWriter.WriteConsoleAsync(
                LogSourceType.FuturesOptionTickDataEvent,
                5003,
                exception.GetErrorMessage()).ConfigureAwait(false);
            logger.LogErrorEvent(
                ServiceId,
                exception,
                "{EventName} for {ContractId}: realtime option trade update failed",
                nameof(FuturesTickTradeDataInsertedEvent),
                source.EntityId.ContractId);
            throw;
        }
    }

    internal static FuturesOptionTickDataV2ReadModel ToOptionTickData(
        FuturesTickTradeDataInsertedEvent source,
        OptionTickerPriceSnapshot optionPrice)
    {
        var quote = optionPrice.Price.Quote;
        var greeks = optionPrice.Greeks;
        return new FuturesOptionTickDataV2ReadModel(
            source.EntityId.ContractId,
            source.EntityId.ValueDate,
            source.TickDataId.SequenceId,
            ToTimeOnly(source.TradeData.EventTimestampNanoseconds),
            decimal.ToDouble(source.TradeData.Price),
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

    static TimeOnly ToTimeOnly(long unixNanoseconds)
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
