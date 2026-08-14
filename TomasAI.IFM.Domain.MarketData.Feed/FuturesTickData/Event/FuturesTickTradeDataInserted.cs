using System.Globalization;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Event;

/// <summary>
/// Handles durable TickAggregation trade insertions for actively leased futures contracts.
/// </summary>
public static class FuturesTickTradeDataInserted
{
    static FuturesTickTradeDataInserted() =>
        ServiceId = $"{LogSourceType.FuturesTickTradeDataInserted}";

    static string ServiceId { get; }

    /// <summary>
    /// Converts the persisted aggregation trade into the legacy futures EOD workflow input.
    /// Contracts that are not futures or are not actively leased by this actor are ignored.
    /// </summary>
    public static async ValueTask<bool> ExecuteAsync(
        this FuturesTickTradeDataInsertedEvent e,
        IEventActorContext context,
        IActorMarketDataFeedCommandApi commandApi,
        FuturesTickDataEventParameters p,
        ILogger<FuturesTickDataEventActor> logger)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(commandApi);
        ArgumentNullException.ThrowIfNull(p);
        ArgumentNullException.ThrowIfNull(logger);

        if (e.AssetTypeId != AssetTypeId.Futures
            || !p.Readers.TryGetReader(e.EntityId.ContractId, out var reader))
            return true;

        try
        {
            var details = reader.GetContractDetails();
            if (details.AssetTypeId != AssetTypeId.Futures
                || !StringComparer.Ordinal.Equals(details.ContractId, e.EntityId.ContractId))
                return true;

            var contract = ToFuturesContract(details);
            var tickData = ToFuturesTickData(e);
            await ExecuteEodWorkflowAsync(
                context,
                commandApi,
                p,
                contract,
                tickData).ConfigureAwait(false);
            return true;
        }
        catch (TickerLeaseNotActiveException)
        {
            // A durable event can arrive after its transient workflow has stopped.
            return true;
        }
        catch (Exception exception)
        {
            await p.StatusConsoleWriter.WriteConsoleAsync(
                LogSourceType.MarketDataFeedEvent,
                6009,
                exception.GetErrorMessage()).ConfigureAwait(false);
            logger.LogErrorEvent(
                ServiceId,
                exception,
                "{EventName} for {ContractId}: futures EOD workflow failed",
                nameof(FuturesTickTradeDataInsertedEvent),
                e.EntityId.ContractId);
            throw;
        }
    }

    /// <summary>
    /// Applies the existing futures or VIX end-of-day workflow using the exact durable trade.
    /// </summary>
    private static async ValueTask ExecuteEodWorkflowAsync(
        IEventActorContext context,
        IActorMarketDataFeedCommandApi commandApi,
        FuturesTickDataEventParameters p,
        FuturesContractV2ReadModel contract,
        FuturesTickDataV2ReadModel tickData)
    {
        var valueDate = tickData.ValueDate;
        if (!contract.Id.IsVixContract)
        {
            var eodDataToday = await context.GetFuturesEodDataAsync(
                contract.ContractId,
                valueDate).ConfigureAwait(false);
            if (eodDataToday is null) return;

            var eodDataRange = await p.BlackboardService.MarketDataFeed.FuturesEodDataRange.GetAsync(
                contract.ContractId,
                valueDate,
                (contractId, startDate, endDate) =>
                    context.GetFuturesEodDataByDateRangeAsync(contractId, startDate, endDate))
                .ConfigureAwait(false);
            var normalCurve = await p.BlackboardService.MarketDataFeed.NormalCurveTable.GetAsync(
                valueDate,
                () => context.GetNormalCurveTableAsync()!)
                .ConfigureAwait(false);
            var vixContractId = p.BlackboardService.MarketDataFeed.VixFuturesContractId.Get(valueDate);
            var vixData = p.BlackboardService.MarketDataFeed.VixFuturesEodData.Get(
                vixContractId!,
                valueDate);
            if (vixData.Count == 0)
            {
                vixData = await context.GetVixFuturesEodDataAsync(
                    vixContractId!,
                    valueDate).ConfigureAwait(false);
                p.BlackboardService.MarketDataFeed.VixFuturesEodData.Set(
                    vixData.First().ContractId,
                    valueDate,
                    vixData);
                if (string.IsNullOrEmpty(vixContractId))
                    p.BlackboardService.MarketDataFeed.VixFuturesContractId.Set(
                        valueDate,
                        vixData.First().ContractId);
            }

            if (eodDataToday.ClosePrice != tickData.Price)
            {
                await commandApi.InsertFuturesEodDataAsync(
                    valueDate,
                    tickData,
                    contract,
                    eodDataToday,
                    eodDataRange,
                    normalCurve!,
                    20,
                    vixData).ConfigureAwait(false);
            }
            return;
        }

        await commandApi.InsertVixFuturesEodDataAsync(tickData).ConfigureAwait(false);
        p.BlackboardService.MarketDataFeed.VixFuturesContractId.Set(
            valueDate,
            tickData.ContractId);
    }

    /// <summary>
    /// Translates provider-neutral cached contract details into the established futures contract read model.
    /// </summary>
    private static FuturesContractV2ReadModel ToFuturesContract(
        TickerContractDetails details) => new(
        details.ContractId,
        details.ProviderContractId,
        details.Ticker,
        details.LocalSymbol,
        string.IsNullOrWhiteSpace(details.SecurityType) ? "FUT" : details.SecurityType,
        details.Currency,
        details.Exchange,
        details.ContractMultiplier.ToString(CultureInfo.InvariantCulture),
        details.MaturityDate,
        details.IsCurrentlyTraded);

    /// <summary>
    /// Translates the durable TickAggregation trade into the established futures tick read model.
    /// </summary>
    private static FuturesTickDataV2ReadModel ToFuturesTickData(
        FuturesTickTradeDataInsertedEvent e) => new(
        e.EntityId.ContractId,
        e.EntityId.ValueDate,
        e.TickDataId.SequenceId,
        ToTimeOnly(e.TradeData.EventTimestampNanoseconds),
        e.TradeData.Price,
        checked((int)e.TradeData.Size));

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
