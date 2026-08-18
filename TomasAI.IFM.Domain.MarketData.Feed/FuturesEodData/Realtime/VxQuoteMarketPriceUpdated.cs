using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime;

/// <summary>
/// Converts an accepted VX quote update into a zero-volume midpoint observation
/// for the rolling VX EOD projection.
/// </summary>
internal static class VxQuoteMarketPriceUpdated
{
    static readonly string ServiceId = $"{LogSourceType.FuturesEodDataEvent}";

    internal static async ValueTask<bool> ExecuteVxQuoteAsync(
        this FuturesMarketPriceUpdatedRealtimeEvent source,
        IMarketDataApi marketDataApi,
        IRealtimeProjector<FuturesEodDataRealtimeActor> projector,
        IStatusConsoleWriter statusConsoleWriter,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(marketDataApi);
        ArgumentNullException.ThrowIfNull(projector);
        ArgumentNullException.ThrowIfNull(statusConsoleWriter);
        ArgumentNullException.ThrowIfNull(logger);

        if (source.UpdateSource != FuturesMarketPriceUpdateSource.Quote
            || source.Price.AssetTypeId != AssetTypeId.Futures
            || !marketDataApi.IsTickDataStreamActive(source.EntityId.ContractId))
            return true;

        try
        {
            ValidateIdentity(source);
            var contract = await marketDataApi.GetFuturesContractAsync(
                    source.EntityId.ContractId)
                .ConfigureAwait(false);
            if (contract is null || !contract.Id.IsVxContract)
                return true;

            if (!TryGetMidpoint(source.Price.Quote, out var midpoint, out var quote))
                return true;

            if (source.Price.Trade is { } trade
                && trade.EventTimestamp >= quote.EventTimestamp)
                return true;

            var tickData = new FuturesTickDataV2ReadModel(
                source.EntityId.ContractId,
                source.EntityId.ValueDate,
                quote.SourceSequence,
                TimeOnly.FromDateTime(quote.EventTimestamp.UtcDateTime),
                midpoint,
                0);
            return await projector.ProcessRealtimeEventAsync(
                    VxFuturesEodDataEventFactory.Create(source, tickData))
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await statusConsoleWriter.WriteConsoleAsync(
                    LogSourceType.MarketDataFeedEvent,
                    6009,
                    exception.GetErrorMessage())
                .ConfigureAwait(false);
            logger.LogErrorEvent(
                ServiceId,
                exception,
                "{EventName} for {ContractId}: realtime VX quote projection failed",
                nameof(FuturesMarketPriceUpdatedRealtimeEvent),
                source.EntityId.ContractId);
            throw;
        }
    }

    static void ValidateIdentity(FuturesMarketPriceUpdatedRealtimeEvent source)
    {
        if (!StringComparer.Ordinal.Equals(
                source.EntityId.ContractId,
                source.Price.ContractId)
            || source.EntityId.ValueDate != source.Price.ValueDate
            || source.EntityId.AssetTypeId != source.Price.AssetTypeId)
        {
            throw new MarketDataContractMappingException(
                source.EntityId.ContractId,
                "the realtime VX quote entity and price snapshot identities do not match");
        }
    }

    static bool TryGetMidpoint(
        FuturesMarketQuoteSnapshot? snapshot,
        out decimal midpoint,
        out FuturesMarketQuoteSnapshot quote)
    {
        if (snapshot is { BidPrice: > 0m, AskPrice: > 0m } value
            && value.BidPrice <= value.AskPrice)
        {
            quote = value;
            midpoint = value.BidPrice.Value
                + ((value.AskPrice.Value - value.BidPrice.Value) / 2m);
            return true;
        }

        quote = default;
        midpoint = default;
        return false;
    }
}
