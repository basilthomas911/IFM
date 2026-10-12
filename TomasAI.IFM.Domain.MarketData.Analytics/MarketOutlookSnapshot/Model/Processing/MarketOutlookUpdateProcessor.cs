using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Realtime;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using CacheComponentType = TomasAI.IFM.Application.MarketData.MarketOutlook.MarketOutlookComponentType;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Model.Processing;

/// <summary>
/// Sole local Market Outlook writer. Multiple producers submit typed updates; this hosted service
/// applies them sequentially, commits immutable snapshots and publishes UI notifications.
/// </summary>
public sealed class MarketOutlookUpdateProcessor(
    IMarketOutlookUpdateWriter writer,
    IMarketOutlookUpdateReader reader,
    IMarketOutlookHotCache readCache,
    IMarketOutlookHotCacheWriter cache,
    IMarketOutlookSnapshotPublisher publisher,
    MarketOutlookProcessorMetrics metrics,
    ILogger<MarketOutlookUpdateProcessor> logger,
    IMarketDataOperationsRecorder? operationsRecorder = null)
    : BackgroundService, IMarketOutlookOperations
{
    int processing;

    public bool IsReady => metrics.GetSnapshot(reader).IsProcessorReady;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        metrics.SetProcessorReady(true);
        try
        {
            await base.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            metrics.SetProcessorReady(false);
            throw;
        }
    }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This actor/service boundary records the exception and contains the failure according to its existing lifecycle or message contract.")] 
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            metrics.SetProcessorReady(true);
            await foreach (var update in reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
            {
                Interlocked.Increment(ref processing);
                try
                {
                    using var generationScope = update.ActorGeneration?.Enter();
                    await ProcessAsync(update, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (OperationCanceledException) when (update.ActorGeneration?.Token.IsCancellationRequested == true)
                {
                    // Retired setup inputs are discarded; newer updates continue immediately.
                }
                catch (Exception exception)
                {
                    Record(new(
                        MarketDataOperationStage.MarketOutlookComposition,
                        MarketDataOperationOutcome.Failed,
                        update.Kind,
                        update.UpdateId,
                        DateTime.UtcNow));
                    global::TomasAI.IFM.Domain.MarketData.Analytics.ActorLifecycleDiagnosticLog.Write18(logger, exception, update.UpdateId, update.Kind);
                }
                finally
                {
                    Interlocked.Decrement(ref processing);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal host shutdown.
        }
        catch (Exception exception)
        {
            global::TomasAI.IFM.Domain.MarketData.Analytics.ActorLifecycleDiagnosticLog.Write19(logger, exception);
        }
        finally
        {
            metrics.SetProcessorReady(false);
        }
    }

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1508", Justification = "Concurrent producers may change PendingCount during the awaited stability interval; both observations are required.")]
    public async ValueTask<bool> WaitForIdleAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.GetTimestamp();
        while (true)
        {
            if (Stopwatch.GetElapsedTime(started) >= timeout)
                return false;
            if (reader.PendingCount == 0 && Volatile.Read(ref processing) == 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken).ConfigureAwait(false);
                if (reader.PendingCount == 0 && Volatile.Read(ref processing) == 0)
                    return true;
            }
            await Task.Delay(TimeSpan.FromMilliseconds(5), cancellationToken).ConfigureAwait(false);
        }
    }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This actor/service boundary records the exception and contains the failure according to its existing lifecycle or message contract.")] 
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        // Let the sole consumer finish already accepted work before the host cancels its loop.
        // This is deliberately bounded so a continuing producer cannot stall host shutdown.
        using var boundedStop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        boundedStop.CancelAfter(TimeSpan.FromSeconds(5));
        var drained = false;
        try
        {
            drained = await WaitForIdleAsync(
                TimeSpan.FromSeconds(5), boundedStop.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The host owns the shutdown deadline.
        }
        catch (OperationCanceledException) when (boundedStop.IsCancellationRequested)
        {
            // The local five-second drain budget elapsed.
        }

        if (!drained)
        {
            global::TomasAI.IFM.Domain.MarketData.Analytics.ActorLifecycleDiagnosticLog.Write14(logger, null, reader.PendingCount);
        }

        try
        {
            await base.StopAsync(boundedStop.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            global::TomasAI.IFM.Domain.MarketData.Analytics.ActorLifecycleDiagnosticLog.Write15(logger, null);
        }
        catch (OperationCanceledException) when (
            boundedStop.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            global::TomasAI.IFM.Domain.MarketData.Analytics.ActorLifecycleDiagnosticLog.Write16(logger, null, reader.PendingCount);
        }
        catch (Exception exception)
        {
            global::TomasAI.IFM.Domain.MarketData.Analytics.ActorLifecycleDiagnosticLog.Write17(logger, exception);
        }
    }

    public MarketOutlookProcessorMetricsSnapshot GetMetrics() => metrics.GetSnapshot(reader);

    public bool RequestRecompose(MarketOutlookEntityId entityId)
    {
        ArgumentNullException.ThrowIfNull(entityId);
        if (!readCache.TryGetInputs(entityId, out var state))
            return false;
        var now = DateTime.UtcNow;
        writer.Submit(new RecomposeMarketOutlookUpdate
        {
            UpdateId = Guid.NewGuid(),
            EntityId = entityId,
            ReceivedAtUtc = now,
            MarketDataAsOfUtc = state.MarketDataAsOfUtc,
            CommandId = Guid.NewGuid(),
            AggregateId = entityId.Format(),
            EventSource = nameof(RequestRecompose)
        });
        return true;
    }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This actor/service boundary records the exception and contains the failure according to its existing lifecycle or message contract.")] 
    async ValueTask ProcessAsync(MarketOutlookUpdate update, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var queueLatency = DateTime.UtcNow - NormalizeUtc(update.ReceivedAtUtc);
        var result = Apply(update);
        RecordComposition(update, started, queueLatency);

        if (update is HydrateMarketOutlookUpdate)
            return;

        var publicationStarted = Stopwatch.GetTimestamp();
        if (!IsPersistable(result.Snapshot))
        {
            global::TomasAI.IFM.Domain.MarketData.Analytics.ActorLifecycleDiagnosticLog.Write20(logger, null, update.EntityId.Format());
            return;
        }
        try
        {
            await publisher.PublishAsync(update, result.Snapshot, cancellationToken).ConfigureAwait(false);
            Record(new(
                MarketDataOperationStage.MarketOutlookPublication,
                MarketDataOperationOutcome.Published,
                update.Kind,
                update.UpdateId,
                DateTime.UtcNow,
                Stopwatch.GetElapsedTime(publicationStarted)));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            cache.RecordNotificationFailure();
            Record(new(
                MarketDataOperationStage.MarketOutlookPublication,
                MarketDataOperationOutcome.Failed,
                update.Kind,
                update.UpdateId,
                DateTime.UtcNow,
                Stopwatch.GetElapsedTime(publicationStarted)));
            global::TomasAI.IFM.Domain.MarketData.Analytics.ActorLifecycleDiagnosticLog.Write21(logger, exception, update.UpdateId, update.Kind);
        }
    }

    /// <summary>Records channel, changed-cache and completed-composition timings after one accepted update.</summary>
    void RecordComposition(MarketOutlookUpdate update, long started, TimeSpan queueLatency)
    {
        Record(new(
            MarketDataOperationStage.MarketOutlookChannel,
            MarketDataOperationOutcome.Applied,
            update.Kind,
            update.UpdateId,
            DateTime.UtcNow,
            queueLatency));
        if (update is not RecomposeMarketOutlookUpdate)
        {
            Record(new(
                MarketDataOperationStage.MarketOutlookCache,
                MarketDataOperationOutcome.Changed,
                update.Kind,
                update.UpdateId,
                DateTime.UtcNow));
        }
        Record(new(
            MarketDataOperationStage.MarketOutlookComposition,
            MarketDataOperationOutcome.Composed,
            update.Kind,
            update.UpdateId,
            DateTime.UtcNow,
            Stopwatch.GetElapsedTime(started)));

    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Optional diagnostics must not reject accepted market updates when a recorder fails.")]
    void Record(in MarketDataOperationMeasurement measurement)
    {
        try { (operationsRecorder ?? metrics).Record(measurement); }
        catch { /* Diagnostics never fail the processor. */ }
    }

    MarketOutlookHotCacheWriteResult Apply(MarketOutlookUpdate update)
    {
        var position = new MarketOutlookSourcePosition(
            update.UpdateId,
            update.SourceSequence,
            NormalizeUtc(update.MarketDataAsOfUtc),
            update.StreamEpochId,
            update.StreamOrdinal);
        var now = DateTime.UtcNow;

        if (update is RsiMarketOutlookUpdate or TdiMarketOutlookUpdate or ItiMarketOutlookUpdate
            or EmaMarketOutlookUpdate or BollingerBandMarketOutlookUpdate or VwapMarketOutlookUpdate
            or AdxMarketOutlookUpdate or AtrMarketOutlookUpdate or MacdMarketOutlookUpdate)
            return ApplyIndicator(update, position, now);

        return update switch
        {
            VixPriceMarketOutlookUpdate value => ApplyVixPrice(value, position, now),
            TradeSignalMarketOutlookUpdate value => Write(
                update, [new(CacheComponentType.TradeSignal, position)],
                state => state with { FuturesTradeSignal = value.Signal },
                MarketOutlookRefreshTrigger.Component, now),
            EodMarketOutlookUpdate value => Write(
                update, [new(CacheComponentType.Eod, position)],
                state => state with { FuturesEodData = value.Eod },
                MarketOutlookRefreshTrigger.EodSession, now),
            EsTradeMarketOutlookUpdate value => ApplyTrade(value, position, now),
            FeedHealthMarketOutlookUpdate value => Write(
                update, [new(CacheComponentType.FeedHealth, position)],
                state => state with { FeedHealth = value.Health, FeedHealthReason = value.Reason },
                MarketOutlookRefreshTrigger.Component, now),
            HistoricalWarmupMarketOutlookUpdate value => Write(
                update,
                [
                    new(CacheComponentType.Ema, position),
                    new(CacheComponentType.BollingerBand, position)
                ],
                state => state with
                {
                    FuturesEmaSignal = value.Ema,
                    FuturesBbSignal = value.BollingerBand
                },
                MarketOutlookRefreshTrigger.Warmup, now),
            HydrateMarketOutlookUpdate value => Write(
                update,
                HydratedComponents(value.Snapshot, position),
                state => Hydrate(state, value.Snapshot),
                MarketOutlookRefreshTrigger.Component,
                now),
            RecomposeMarketOutlookUpdate => Write(
                update, [], static state => state,
                MarketOutlookRefreshTrigger.Component, now),
            _ => throw new ArgumentOutOfRangeException(
                nameof(update), update.GetType().FullName, "Unknown Market Outlook update type")
        };
    }

    /// <summary>Updates the latest VX mark and initializes its provisional session open only when no positive open has been observed.</summary>
    MarketOutlookHotCacheWriteResult ApplyVixPrice(VixPriceMarketOutlookUpdate value, MarketOutlookSourcePosition position, DateTime now)
        => Write(
                value, [new(CacheComponentType.Vx, position)],
                state => state with
                {
                    VixFuturesPrice = value.Price is > 0m
                        ? value.Price
                        : state.VixFuturesPrice,
                    // The first accepted VX observation is a provisional session open. A later
                    // official statistic replaces it, so a delayed statistics feed never leaves
                    // Price Volatility permanently Unknown.
                    VixFuturesSessionOpenPrice = (value.SessionOpenPrice is> 0m) switch
{
    true => value.SessionOpenPrice,
    false => (state.VixFuturesSessionOpenPrice is> 0m) switch
    {
        true => state.VixFuturesSessionOpenPrice,
        false => value.Price is> 0m ? value.Price : null
    }}
                },
                MarketOutlookRefreshTrigger.Component, now);

    /// <summary>Applies one technical-indicator observation to its typed cache component with the same source fence.</summary>
    MarketOutlookHotCacheWriteResult ApplyIndicator(MarketOutlookUpdate update, MarketOutlookSourcePosition position, DateTime now)
        => update switch
        {
            RsiMarketOutlookUpdate value => Write(
                update, [new(CacheComponentType.Rsi, position)],
                state => state with { FuturesRsiSignal = value.Signal },
                MarketOutlookRefreshTrigger.Component, now),
            TdiMarketOutlookUpdate value => Write(
                update, [new(CacheComponentType.Tdi, position)],
                state => state with { FuturesTdiSignal = value.Signal },
                MarketOutlookRefreshTrigger.Component, now),
            ItiMarketOutlookUpdate value => Write(
                update, ItiComponents(value.Signal, position),
                state => MergeIti(state, value.Signal),
                MarketOutlookRefreshTrigger.Component, now),
            EmaMarketOutlookUpdate value => Write(
                update, [new(CacheComponentType.Ema, position)],
                state => state with { FuturesEmaSignal = value.Signal },
                MarketOutlookRefreshTrigger.Component, now),
            BollingerBandMarketOutlookUpdate value => Write(
                update, [new(CacheComponentType.BollingerBand, position)],
                state => state with { FuturesBbSignal = value.Signal },
                MarketOutlookRefreshTrigger.Component, now),
            VwapMarketOutlookUpdate value => Write(
                update, [new(CacheComponentType.Vwap, position)],
                state => state with { FuturesVwapSignal = value.Signal },
                MarketOutlookRefreshTrigger.Component, now),
            AdxMarketOutlookUpdate value => Write(
                update, [new(CacheComponentType.Adx, position)],
                state => state with { FuturesAdxSignal = value.Signal },
                MarketOutlookRefreshTrigger.Component, now),
            AtrMarketOutlookUpdate value => Write(
                update, [new(CacheComponentType.Atr, position)],
                state => state with { FuturesAtrSignal = value.Signal },
                MarketOutlookRefreshTrigger.Component, now),
            MacdMarketOutlookUpdate value => Write(
                update, [new(CacheComponentType.Macd, position)],
                state => state with { FuturesMacdSignal = value.Signal },
                MarketOutlookRefreshTrigger.Component, now),
            _ => throw new ArgumentOutOfRangeException(nameof(update), update.GetType().FullName, "Unknown indicator update type")
        };

    static MarketOutlookInputState Hydrate(
        MarketOutlookInputState state,
        MarketOutlookReadModel snapshot) => state with
        {
            FuturesEodData = snapshot.FuturesEodData.IsValid ? snapshot.FuturesEodData : state.FuturesEodData,
            FuturesTradeSignal = snapshot.FuturesTradeSignal ?? state.FuturesTradeSignal,
            FuturesRsiSignal = snapshot.FuturesRsiSignal ?? state.FuturesRsiSignal,
            FuturesTdiSignal = snapshot.FuturesTdiSignal ?? state.FuturesTdiSignal,
            TrendDirectionChange = snapshot.TrendDirectionChange ?? state.TrendDirectionChange,
            TrendExtremeChange = snapshot.TrendExtremeChange ?? state.TrendExtremeChange,
            TrendReversalChange = snapshot.TrendReversalChange ?? state.TrendReversalChange,
            LatestItiTrendSignal = snapshot.LatestItiTrendSignal ?? state.LatestItiTrendSignal,
            VixFuturesPrice = snapshot.VixFuturesPrice is > 0m ? snapshot.VixFuturesPrice : state.VixFuturesPrice,
            VixFuturesSessionOpenPrice = snapshot.VixFuturesPrice is > 0m
            ? snapshot.VixFuturesPrice
            : state.VixFuturesSessionOpenPrice,
            FuturesEmaSignal = snapshot.FuturesEmaSignal ?? state.FuturesEmaSignal,
            FuturesBbSignal = snapshot.FuturesBbSignal ?? state.FuturesBbSignal,
            FuturesVwapSignal = snapshot.FuturesVwapSignal ?? state.FuturesVwapSignal,
            FuturesAdxSignal = snapshot.FuturesAdxSignal ?? state.FuturesAdxSignal,
            FuturesAtrSignal = snapshot.FuturesAtrSignal ?? state.FuturesAtrSignal,
            FuturesMacdSignal = snapshot.FuturesMacdSignal ?? state.FuturesMacdSignal,
            CurrentEsPrice = (snapshot.FuturesTradeSignal?.FuturesPrice is> 0d) switch
{
    true => (decimal)snapshot.FuturesTradeSignal.FuturesPrice,
    false => snapshot.FuturesEodData.ClosePrice is> 0m ? snapshot.FuturesEodData.ClosePrice : state.CurrentEsPrice
},
            MarketDataAsOfUtc = snapshot.MarketDataAsOfUtc > state.MarketDataAsOfUtc
            ? snapshot.MarketDataAsOfUtc
            : state.MarketDataAsOfUtc,
            FeedHealth = string.IsNullOrWhiteSpace(snapshot.FeedHealth) ? state.FeedHealth : snapshot.FeedHealth,
            FeedHealthReason = string.IsNullOrWhiteSpace(snapshot.FeedHealthReason)
            ? state.FeedHealthReason
            : snapshot.FeedHealthReason
        };

    static List<MarketOutlookComponentWrite> HydratedComponents(
        MarketOutlookReadModel snapshot,
        MarketOutlookSourcePosition position)
    {
        List<MarketOutlookComponentWrite> components = [];
        if (snapshot.FuturesEodData.IsValid) components.Add(new(CacheComponentType.Eod, position));
        if (snapshot.FuturesTradeSignal is not null) components.Add(new(CacheComponentType.TradeSignal, position));
        if (snapshot.FuturesRsiSignal is not null) components.Add(new(CacheComponentType.Rsi, position));
        if (snapshot.FuturesTdiSignal is not null) components.Add(new(CacheComponentType.Tdi, position));
        if (snapshot.LatestItiTrendSignal is not null) components.Add(new(CacheComponentType.ItiLatest, position));
        if (snapshot.TrendDirectionChange is not null) components.Add(new(CacheComponentType.ItiDirection, position));
        if (snapshot.TrendExtremeChange is not null) components.Add(new(CacheComponentType.ItiExtreme, position));
        if (snapshot.TrendReversalChange is not null) components.Add(new(CacheComponentType.ItiReversal, position));
        if (snapshot.VixFuturesPrice is > 0m) components.Add(new(CacheComponentType.Vx, position));
        if (snapshot.FuturesEmaSignal is not null) components.Add(new(CacheComponentType.Ema, position));
        if (snapshot.FuturesBbSignal is not null) components.Add(new(CacheComponentType.BollingerBand, position));
        if (snapshot.FuturesVwapSignal is not null) components.Add(new(CacheComponentType.Vwap, position));
        if (snapshot.FuturesAdxSignal is not null) components.Add(new(CacheComponentType.Adx, position));
        if (snapshot.FuturesAtrSignal is not null) components.Add(new(CacheComponentType.Atr, position));
        if (snapshot.FuturesMacdSignal is not null) components.Add(new(CacheComponentType.Macd, position));
        if (!string.IsNullOrWhiteSpace(snapshot.FeedHealth)) components.Add(new(CacheComponentType.FeedHealth, position));
        if (snapshot.FuturesTradeSignal?.FuturesPrice is > 0d
            || snapshot.FuturesEodData.ClosePrice is > 0m)
            components.Add(new(CacheComponentType.EsTrade, position));
        return components;
    }

    MarketOutlookHotCacheWriteResult ApplyTrade(
        EsTradeMarketOutlookUpdate update,
        MarketOutlookSourcePosition position,
        DateTime now)
    {
        var trade = update.PriceUpdate.Price.Trade!.Value;
        MarketOutlookDailyPreviewCalculator.TryCalculate(update.PriceUpdate, out var liveEma, out var liveBb);
        return Write(
            update,
            [new(CacheComponentType.EsTrade, position)],
            state => state with
            {
                CurrentEsPrice = trade.LastPrice,
                FuturesEmaSignal = liveEma ?? state.FuturesEmaSignal,
                FuturesBbSignal = liveBb ?? state.FuturesBbSignal
            },
            MarketOutlookRefreshTrigger.EsTrade,
            now);
    }

    MarketOutlookHotCacheWriteResult Write(
        MarketOutlookUpdate update,
        IReadOnlyCollection<MarketOutlookComponentWrite> components,
        Func<MarketOutlookInputState, MarketOutlookInputState> merge,
        MarketOutlookRefreshTrigger trigger,
        DateTime now) => cache.Write(
            update.EntityId,
            components,
            merge,
            state => MarketOutlookComposer.Compose(state, trigger, now));

    static List<MarketOutlookComponentWrite> ItiComponents(
        FuturesItiSignalV2ReadModel signal,
        MarketOutlookSourcePosition position)
    {
        List<MarketOutlookComponentWrite> components = [new(CacheComponentType.ItiLatest, position)];
        var milestone = signal.IntrinsicTimeMode switch
        {
            IntrinsicTimeModeType.TrendDirectionChanged => CacheComponentType.ItiDirection,
            IntrinsicTimeModeType.TrendExtremeChanged => CacheComponentType.ItiExtreme,
            IntrinsicTimeModeType.TrendReversalChanged => CacheComponentType.ItiReversal,
            _ => (CacheComponentType?)null
        };
        if (milestone is { } value)
            components.Add(new(value, position));
        return components;
    }

    static MarketOutlookInputState MergeIti(
        MarketOutlookInputState state,
        FuturesItiSignalV2ReadModel signal) => state with
        {
            LatestItiTrendSignal = signal,
            TrendDirectionChange = signal.IntrinsicTimeMode == IntrinsicTimeModeType.TrendDirectionChanged
            ? signal : state.TrendDirectionChange,
            TrendExtremeChange = signal.IntrinsicTimeMode == IntrinsicTimeModeType.TrendExtremeChanged
            ? signal : state.TrendExtremeChange,
            TrendReversalChange = signal.IntrinsicTimeMode == IntrinsicTimeModeType.TrendReversalChanged
            ? signal : state.TrendReversalChange
        };

    static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    static bool IsPersistable(MarketOutlookReadModel snapshot)
    {
        var eod = snapshot.FuturesEodData;
        return !string.IsNullOrWhiteSpace(snapshot.ContractId)
            && snapshot.ValueDate != default
            && string.Equals(eod.Symbol, "ES", StringComparison.OrdinalIgnoreCase)
            && string.Equals(eod.ContractId, snapshot.ContractId, StringComparison.Ordinal)
            && eod.ValueDate == snapshot.ValueDate
            && eod.OpenPrice > 0m
            && eod.HighPrice > 0m
            && eod.LowPrice > 0m
            && eod.ClosePrice > 0m
            && eod.HighPrice >= eod.LowPrice
            && eod.OpenPrice >= eod.LowPrice
            && eod.OpenPrice <= eod.HighPrice
            && eod.ClosePrice >= eod.LowPrice
            && eod.ClosePrice <= eod.HighPrice
            && snapshot.UpdatedAtUtc != default
            && snapshot.UpdatedAtUtc.Kind == DateTimeKind.Utc
            && snapshot.MarketDataAsOfUtc != default
            && snapshot.MarketDataAsOfUtc.Kind == DateTimeKind.Utc;
    }
}
