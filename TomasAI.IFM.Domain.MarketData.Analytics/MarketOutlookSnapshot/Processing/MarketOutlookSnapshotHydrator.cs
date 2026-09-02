using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Processing;

public interface IMarketOutlookSnapshotHydrator
{
    ValueTask<MarketOutlookReadModel?> HydrateAsync(
        MarketOutlookEntityId entityId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Writes the latest durable source projections as the explicit UI-startup baseline.
/// Persisted components replace the current cache at this boundary; live updates received after
/// this write replace the baseline through the normal single-writer processor.
/// </summary>
public sealed class MarketOutlookSnapshotHydrator(
    IDbContextFactory dbFactory,
    IMarketOutlookUpdateWriter writer,
    IMarketOutlookOperations operations,
    IMarketOutlookHotCache cache,
    ILogger<MarketOutlookSnapshotHydrator> logger)
    : IMarketOutlookSnapshotHydrator
{
    static readonly MarketSeriesIdentity EsDailySeries = MarketSeriesIdentity.ForFuturesSeries(
        new FuturesSeriesId("ES", "calendar-front", "unadjusted", 1));

    public async ValueTask<MarketOutlookReadModel?> HydrateAsync(
        MarketOutlookEntityId entityId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entityId);
        var db = dbFactory.MarketDataDb;

        var eodTask = LoadAsync(
            () => db.GetLastFuturesEodDataAsync(entityId.ContractId, entityId.ValueDate),
            "EOD", entityId);
        var tradeSignalTask = LoadAsync(
            () => db.GetLastFuturesTradeSignalAsync(
                entityId.ContractId, entityId.ValueDate, cancellationToken),
            "trade signal", entityId);
        var rsiTask = LoadAsync(
            () => db.GetLastFuturesRsiSignalAsync(
                entityId.ContractId, entityId.ValueDate, TimeFrameType.FifteenSeconds, 14,
                cancellationToken),
            "RSI", entityId);
        var tdiTask = LoadAsync(
            () => db.GetLastFuturesTdiSignalAsync(
                entityId.ContractId,
                entityId.ValueDate,
                TimeFrameType.FifteenSeconds,
                FuturesTdiConfiguration.StandardConfigurationId,
                cancellationToken),
            "TDI", entityId);
        var itiLatestTask = LoadAsync(
            () => db.GetLastFuturesItiSignalAsync(
                entityId.ContractId,
                entityId.ValueDate,
                TimeFrameType.Daily,
                cancellationToken),
            "latest ITI", entityId);
        var itiDirectionTask = LoadAsync(
            () => db.GetLastFuturesItiSignalTrendDirectionChangeAsync(
                entityId.ContractId, entityId.ValueDate, cancellationToken),
            "ITI direction", entityId);
        var itiExtremeTask = LoadAsync(
            () => db.GetLastFuturesItiSignalTrendExtremeChangeAsync(
                entityId.ContractId, entityId.ValueDate, cancellationToken),
            "ITI extreme", entityId);
        var itiReversalTask = LoadAsync(
            () => db.GetLastFuturesItiSignalTrendReversalChangeAsync(
                entityId.ContractId, entityId.ValueDate, cancellationToken),
            "ITI reversal", entityId);
        var emaTask = LoadDailyEmaAsync(db, entityId, cancellationToken);
        var bbTask = LoadDailyBollingerBandAsync(db, entityId, cancellationToken);
        var vixBaselineTask = LoadVixBaselineSafelyAsync(entityId, cancellationToken);

        await Task.WhenAll(
            eodTask, tradeSignalTask, rsiTask, tdiTask, itiLatestTask, itiDirectionTask,
            itiExtremeTask, itiReversalTask, emaTask, bbTask, vixBaselineTask).ConfigureAwait(false);

        var eod = await eodTask.ConfigureAwait(false);
        var tradeSignal = await tradeSignalTask.ConfigureAwait(false);
        var rsi = await rsiTask.ConfigureAwait(false);
        var tdi = await tdiTask.ConfigureAwait(false);
        var itiLatest = await itiLatestTask.ConfigureAwait(false);
        var itiDirection = await itiDirectionTask.ConfigureAwait(false);
        var itiExtreme = await itiExtremeTask.ConfigureAwait(false);
        var itiReversal = await itiReversalTask.ConfigureAwait(false);
        var ema = await emaTask.ConfigureAwait(false);
        var bb = await bbTask.ConfigureAwait(false);
        var vixBaseline = await vixBaselineTask.ConfigureAwait(false);

        var asOfUtc = LatestTimestamp(
            entityId.ValueDate, rsi, tdi, itiLatest, itiDirection, itiExtreme,
            itiReversal, ema, bb);
        var baseline = new MarketOutlookInputState
        {
            EntityId = entityId,
            FuturesEodData = eod,
            FuturesTradeSignal = tradeSignal,
            FuturesRsiSignal = rsi,
            FuturesTdiSignal = tdi,
            LatestItiTrendSignal = itiLatest,
            TrendDirectionChange = itiDirection,
            TrendExtremeChange = itiExtreme,
            TrendReversalChange = itiReversal,
            FuturesEmaSignal = ema,
            FuturesBbSignal = bb,
            VixFuturesSessionOpenPrice = vixBaseline?.SessionOpenPrice,
            VixFuturesPrice = vixBaseline?.CurrentPrice,
            CurrentEsPrice = tradeSignal?.FuturesPrice > 0d
                ? Convert.ToDecimal(tradeSignal.FuturesPrice)
                : null,
            MarketDataAsOfUtc = asOfUtc
        };

        if (!HasAnyValue(baseline))
        {
            cache.TryGetCurrent(entityId, out var current);
            return current;
        }

        var updateId = Guid.NewGuid();
        writer.Submit(new HydrateMarketOutlookUpdate
        {
            UpdateId = updateId,
            EntityId = entityId,
            ReceivedAtUtc = DateTime.UtcNow,
            MarketDataAsOfUtc = asOfUtc,
            CommandId = updateId,
            AggregateId = entityId.Format(),
            EventSource = nameof(MarketOutlookSnapshotHydrator),
            Baseline = baseline
        });
        await operations.WaitForIdleAsync(TimeSpan.FromSeconds(5), cancellationToken)
            .ConfigureAwait(false);
        cache.TryGetCurrent(entityId, out var hydrated);
        return hydrated;
    }

    async Task<FuturesEmaSignalReadModel?> LoadDailyEmaAsync(
        TomasAI.IFM.Application.Storage.MarketDataDb.IMarketDataDbContext db,
        MarketOutlookEntityId entityId,
        CancellationToken cancellationToken)
    {
        var continuation = await LoadAsync(
            () => db.GetLatestFuturesEmaSignalAsync(
                EsDailySeries, entityId.ValueDate, cancellationToken),
            "daily EMA continuation", entityId).ConfigureAwait(false);
        return continuation ?? await LoadAsync(
            () => db.GetLatestFuturesEmaSignalAsync(
                MarketSeriesIdentity.ForContract(entityId.ContractId),
                entityId.ValueDate,
                cancellationToken),
            "daily EMA contract", entityId).ConfigureAwait(false);
    }

    async Task<FuturesBbSignalReadModel?> LoadDailyBollingerBandAsync(
        TomasAI.IFM.Application.Storage.MarketDataDb.IMarketDataDbContext db,
        MarketOutlookEntityId entityId,
        CancellationToken cancellationToken)
    {
        var continuation = await LoadAsync(
            () => db.GetLatestFuturesBollingerBandSignalAsync(
                EsDailySeries, entityId.ValueDate, cancellationToken),
            "daily Bollinger continuation", entityId).ConfigureAwait(false);
        return continuation ?? await LoadAsync(
            () => db.GetLatestFuturesBollingerBandSignalAsync(
                MarketSeriesIdentity.ForContract(entityId.ContractId),
                entityId.ValueDate,
                cancellationToken),
            "daily Bollinger contract", entityId).ConfigureAwait(false);
    }

    async Task<VixFuturesBaseline?> LoadVixBaselineAsync(
        DateOnly valueDate,
        CancellationToken cancellationToken)
    {
        var contracts = await dbFactory.SecuritiesDb
            .GetCurrentlyTradedFuturesContractsAsync("VX", cancellationToken)
            .ConfigureAwait(false);
        var contractId = contracts.FirstOrDefault(value =>
            value.CurrentlyTraded
            && string.Equals(value.Symbol, "VX", StringComparison.OrdinalIgnoreCase))?.ContractId;
        if (string.IsNullOrWhiteSpace(contractId))
            return null;
        var eod = await dbFactory.MarketDataDb
            .GetLastVixFuturesEodDataAsync(contractId, valueDate)
            .ConfigureAwait(false);
        return eod is { OpenPrice: > 0m, ClosePrice: > 0m }
            ? new VixFuturesBaseline(eod.OpenPrice, eod.ClosePrice)
            : null;
    }

    async Task<VixFuturesBaseline?> LoadVixBaselineSafelyAsync(
        MarketOutlookEntityId entityId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await LoadVixBaselineAsync(entityId.ValueDate, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Could not hydrate persisted Market Outlook VX EOD baseline for {EntityId}; other components continue",
                entityId.Format());
            return null;
        }
    }

    async Task<T?> LoadAsync<T>(
        Func<Task<T?>> load,
        string component,
        MarketOutlookEntityId entityId)
        where T : class
    {
        try
        {
            return await load().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Could not hydrate persisted Market Outlook {Component} for {EntityId}; other components continue",
                component,
                entityId.Format());
            return null;
        }
    }

    static bool HasAnyValue(MarketOutlookInputState value) =>
        value.FuturesEodData is not null
        || value.FuturesTradeSignal is not null
        || value.FuturesRsiSignal is not null
        || value.FuturesTdiSignal is not null
        || value.LatestItiTrendSignal is not null
        || value.TrendDirectionChange is not null
        || value.TrendExtremeChange is not null
        || value.TrendReversalChange is not null
        || value.VixFuturesSessionOpenPrice is > 0
        || value.VixFuturesPrice is > 0
        || value.FuturesEmaSignal is not null
        || value.FuturesBbSignal is not null;

    readonly record struct VixFuturesBaseline(
        decimal SessionOpenPrice,
        decimal CurrentPrice);

    static DateTime LatestTimestamp(
        DateOnly valueDate,
        FuturesRsiSignalReadModel? rsi,
        FuturesTdiSignalReadModel? tdi,
        params object?[] signals)
    {
        var latest = valueDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        if (rsi?.Metadata?.MarketDataAsOfUtc.UtcDateTime is { } rsiTimestamp
            && rsiTimestamp > latest)
            latest = rsiTimestamp;
        else if (rsi is not null)
            latest = Max(latest, valueDate.ToDateTime(rsi.Timestamp, DateTimeKind.Utc));
        if (tdi is not null)
            latest = Max(latest, valueDate.ToDateTime(tdi.Timestamp, DateTimeKind.Utc));
        foreach (var signal in signals)
        {
            latest = signal switch
            {
                FuturesItiSignalV2ReadModel iti =>
                    Max(latest, NormalizeUtc(iti.IntrinsicTime)),
                FuturesEmaSignalReadModel ema =>
                    Max(latest, ema.Metadata.MarketDataAsOfUtc.UtcDateTime),
                FuturesBbSignalReadModel bb =>
                    Max(latest, bb.Metadata.MarketDataAsOfUtc.UtcDateTime),
                _ => latest
            };
        }
        return latest;
    }

    static DateTime Max(DateTime left, DateTime right) => right > left ? right : left;

    static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
