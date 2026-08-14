using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Event;

/// <summary>
/// Handles durable TickAggregation trade insertions for active futures streams.
/// </summary>
public static class FuturesTickTradeDataInserted
{
    static FuturesTickTradeDataInserted() =>
        ServiceId = $"{LogSourceType.FuturesTickTradeDataInserted}";

    static string ServiceId { get; }

    /// <summary>
    /// Converts the persisted aggregation trade into the legacy futures EOD workflow input.
    /// Contracts that are not futures or are not active for this actor are ignored.
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
            || !p.Streams.TryGetContract(e.EntityId.ContractId, out var contract)
            || !p.MarketDataApi.IsTickDataStreamActive(e.EntityId.ContractId))
            return true;

        try
        {
            if (!StringComparer.Ordinal.Equals(contract.ContractId, e.EntityId.ContractId))
                return true;

            var tickData = ToFuturesTickData(e);
            await ExecuteEodWorkflowAsync(
                context,
                commandApi,
                p,
                contract,
                tickData).ConfigureAwait(false);
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
