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
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using CacheComponentType = TomasAI.IFM.Application.MarketData.MarketOutlook.MarketOutlookComponentType;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Model.Processing;

public sealed record RsiMarketOutlookUpdate : MarketOutlookUpdate
{
    public override MarketOutlookUpdateKind Kind => MarketOutlookUpdateKind.Rsi;
    public required FuturesRsiSignalReadModel Signal { get; init; }
}

public sealed record TdiMarketOutlookUpdate : MarketOutlookUpdate
{
    public override MarketOutlookUpdateKind Kind => MarketOutlookUpdateKind.Tdi;
    public required FuturesTdiSignalReadModel Signal { get; init; }
}

public sealed record ItiMarketOutlookUpdate : MarketOutlookUpdate
{
    public override MarketOutlookUpdateKind Kind => MarketOutlookUpdateKind.Iti;
    public required FuturesItiSignalV2ReadModel Signal { get; init; }
}

public sealed record EmaMarketOutlookUpdate : MarketOutlookUpdate
{
    public override MarketOutlookUpdateKind Kind => MarketOutlookUpdateKind.Ema;
    public required FuturesEmaSignalReadModel Signal { get; init; }
}

public sealed record BollingerBandMarketOutlookUpdate : MarketOutlookUpdate
{
    public override MarketOutlookUpdateKind Kind => MarketOutlookUpdateKind.BollingerBand;
    public required FuturesBbSignalReadModel Signal { get; init; }
}

public sealed record EsTradeMarketOutlookUpdate : MarketOutlookUpdate
{
    public override MarketOutlookUpdateKind Kind => MarketOutlookUpdateKind.EsTrade;
    public required FuturesMarketPriceUpdatedRealtimeEvent PriceUpdate { get; init; }
}

public sealed record VixPriceMarketOutlookUpdate : MarketOutlookUpdate
{
    public override MarketOutlookUpdateKind Kind => MarketOutlookUpdateKind.VixPrice;
    public required decimal? Price { get; init; }
    public decimal? SessionOpenPrice { get; init; }
}

public sealed record EodMarketOutlookUpdate : MarketOutlookUpdate
{
    public override MarketOutlookUpdateKind Kind => MarketOutlookUpdateKind.Eod;
    public required FuturesEodDataV2ReadModel Eod { get; init; }
}

public sealed record TradeSignalMarketOutlookUpdate : MarketOutlookUpdate
{
    public override MarketOutlookUpdateKind Kind => MarketOutlookUpdateKind.TradeSignal;
    public required FuturesTradeSignalV2ReadModel Signal { get; init; }
}

public sealed record FeedHealthMarketOutlookUpdate : MarketOutlookUpdate
{
    public override MarketOutlookUpdateKind Kind => MarketOutlookUpdateKind.FeedHealth;
    public required string Health { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public sealed record HistoricalWarmupMarketOutlookUpdate : MarketOutlookUpdate
{
    public override MarketOutlookUpdateKind Kind => MarketOutlookUpdateKind.HistoricalWarmup;
    public required FuturesEmaSignalReadModel Ema { get; init; }
    public required FuturesBbSignalReadModel BollingerBand { get; init; }
}

/// <summary>
/// Seeds a new process-local cache from the latest durable snapshot before incremental startup
/// updates are applied. Hydration itself is not republished; the following warmup update publishes
/// the merged snapshot.
/// </summary>
public sealed record HydrateMarketOutlookUpdate : MarketOutlookUpdate
{
    public override MarketOutlookUpdateKind Kind => MarketOutlookUpdateKind.Hydration;
    public required MarketOutlookReadModel Snapshot { get; init; }
}

public sealed record RecomposeMarketOutlookUpdate : MarketOutlookUpdate
{
    public override MarketOutlookUpdateKind Kind => MarketOutlookUpdateKind.Recompose;
}

public interface IMarketOutlookSnapshotCommandWriter
{
    ValueTask PublishAsync(
        MarketOutlookUpdate update,
        MarketOutlookReadModel snapshot,
        CancellationToken cancellationToken);
}

/// <summary>Sends each complete local snapshot through the sole durable insert command.</summary>
public sealed class ActorMarketOutlookSnapshotCommandWriter
    : IMarketOutlookSnapshotCommandWriter
{
    static readonly ActorMailboxId PublisherId = new(
        ActorType.Command,
        InsertMarketOutlookSnapshotCommand.Actor);
    IActorProducer? producer;
    readonly IActorSupervisor supervisor;
    readonly MarketOutlookSnapshotPersistencePolicy persistencePolicy;

    public ActorMarketOutlookSnapshotCommandWriter(IActorSupervisor supervisor)
        : this(supervisor, MarketOutlookSnapshotPersistencePolicy.Legacy)
    {
    }

    public ActorMarketOutlookSnapshotCommandWriter(
        IActorSupervisor supervisor,
        MarketOutlookSnapshotPersistencePolicy persistencePolicy)
    {
        this.supervisor = supervisor ?? throw new ArgumentNullException(nameof(supervisor));
        this.persistencePolicy = persistencePolicy
            ?? throw new ArgumentNullException(nameof(persistencePolicy));
    }

    public async ValueTask PublishAsync(
        MarketOutlookUpdate update,
        MarketOutlookReadModel snapshot,
        CancellationToken cancellationToken)
    {
        var durableSnapshot = snapshot with
        {
            SnapshotSource = persistencePolicy.SnapshotSource
        };
        var command = new InsertMarketOutlookSnapshotCommand(durableSnapshot) with
        {
            CommandId = update.UpdateId == Guid.Empty ? Guid.NewGuid() : update.UpdateId
        };
        var result = await (producer ??= supervisor.GetProducer(PublisherId))
            .RequestAsync<InsertMarketOutlookSnapshotCommand, MarketOutlookEntityId, GuidResult>(
                command.Subject,
                command,
                command.EntityId,
                cancellationToken)
            .ConfigureAwait(false);
        if (result?.Success != true)
            throw new InvalidOperationException(
                result?.ErrorMessage ?? "Market Outlook snapshot insert command failed.");
    }
}

/// <summary>
/// Sole local Market Outlook writer. Multiple producers submit typed updates; this hosted service
/// applies them sequentially, commits immutable snapshots and publishes UI notifications.
/// </summary>
public sealed class MarketOutlookUpdateProcessor(
    IMarketOutlookUpdateWriter writer,
    IMarketOutlookUpdateReader reader,
    IMarketOutlookHotCache readCache,
    IMarketOutlookHotCacheWriter cache,
    IMarketOutlookSnapshotCommandWriter publisher,
    MarketOutlookProcessorMetrics metrics,
    ILogger<MarketOutlookUpdateProcessor> logger)
    : BackgroundService, IMarketOutlookOperations
{
    int processing;

    public bool IsReady => metrics.GetSnapshot(reader).IsProcessorReady;

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
                    await ProcessAsync(update, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    metrics.Record(new(
                        MarketDataOperationStage.MarketOutlookComposition,
                        MarketDataOperationOutcome.Failed,
                        update.Kind,
                        update.UpdateId,
                        DateTime.UtcNow));
                    logger.LogError(
                        exception,
                        "Market Outlook update {UpdateId} ({UpdateKind}) failed; processing continues",
                        update.UpdateId,
                        update.Kind);
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
            logger.LogError(
                exception,
                "Market Outlook update processor failed unexpectedly; the API host will remain running");
        }
        finally
        {
            metrics.SetProcessorReady(false);
        }
    }

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
            logger.LogWarning(
                "Market Outlook processor shutdown drain expired with {UndrainedCount} updates outstanding",
                reader.PendingCount);
        }

        try
        {
            await base.StopAsync(boundedStop.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Market Outlook processor stopped at the API shutdown deadline");
        }
        catch (OperationCanceledException) when (
            boundedStop.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "Market Outlook processor exceeded its bounded shutdown deadline with {UndrainedCount} updates outstanding",
                reader.PendingCount);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Market Outlook processor shutdown failed unexpectedly; API shutdown will continue");
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

    async ValueTask ProcessAsync(MarketOutlookUpdate update, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var queueLatency = DateTime.UtcNow - NormalizeUtc(update.ReceivedAtUtc);
        var result = Apply(update);
        metrics.Record(new(
            MarketDataOperationStage.MarketOutlookChannel,
            MarketDataOperationOutcome.Applied,
            update.Kind,
            update.UpdateId,
            DateTime.UtcNow,
            queueLatency));
        if (update is not RecomposeMarketOutlookUpdate)
        {
            metrics.Record(new(
                MarketDataOperationStage.MarketOutlookCache,
                MarketDataOperationOutcome.Changed,
                update.Kind,
                update.UpdateId,
                DateTime.UtcNow));
        }
        metrics.Record(new(
            MarketDataOperationStage.MarketOutlookComposition,
            MarketDataOperationOutcome.Composed,
            update.Kind,
            update.UpdateId,
            DateTime.UtcNow,
            Stopwatch.GetElapsedTime(started)));

        if (update is HydrateMarketOutlookUpdate)
            return;

        var publicationStarted = Stopwatch.GetTimestamp();
        if (!IsPersistable(result.Snapshot))
        {
            logger.LogDebug(
                "Skipped durable Market Outlook snapshot for {EntityId}: a positive, internally consistent ES OHLC baseline is not available",
                update.EntityId.Format());
            return;
        }
        try
        {
            await publisher.PublishAsync(update, result.Snapshot, cancellationToken).ConfigureAwait(false);
            metrics.Record(new(
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
            metrics.Record(new(
                MarketDataOperationStage.MarketOutlookPublication,
                MarketDataOperationOutcome.Failed,
                update.Kind,
                update.UpdateId,
                DateTime.UtcNow,
                Stopwatch.GetElapsedTime(publicationStarted)));
            logger.LogError(
                exception,
                "Market Outlook notification failed after cache commit for update {UpdateId} ({UpdateKind})",
                update.UpdateId,
                update.Kind);
        }
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

        return update switch
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
            VixPriceMarketOutlookUpdate value => Write(
                update, [new(CacheComponentType.Vx, position)],
                state => state with
                {
                    VixFuturesPrice = value.Price is > 0m
                        ? value.Price
                        : state.VixFuturesPrice,
                    // The first accepted VX observation is a provisional session open. A later
                    // official statistic replaces it, so a delayed statistics feed never leaves
                    // Price Volatility permanently Unknown.
                    VixFuturesSessionOpenPrice = value.SessionOpenPrice is > 0m
                        ? value.SessionOpenPrice
                        : state.VixFuturesSessionOpenPrice is > 0m
                            ? state.VixFuturesSessionOpenPrice
                            : value.Price is > 0m ? value.Price : null
                },
                MarketOutlookRefreshTrigger.Component, now),
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
        CurrentEsPrice = snapshot.FuturesTradeSignal?.FuturesPrice is > 0d
            ? (decimal)snapshot.FuturesTradeSignal.FuturesPrice
            : snapshot.FuturesEodData.ClosePrice is > 0m
                ? snapshot.FuturesEodData.ClosePrice
                : state.CurrentEsPrice,
        MarketDataAsOfUtc = snapshot.MarketDataAsOfUtc > state.MarketDataAsOfUtc
            ? snapshot.MarketDataAsOfUtc
            : state.MarketDataAsOfUtc,
        FeedHealth = string.IsNullOrWhiteSpace(snapshot.FeedHealth) ? state.FeedHealth : snapshot.FeedHealth,
        FeedHealthReason = string.IsNullOrWhiteSpace(snapshot.FeedHealthReason)
            ? state.FeedHealthReason
            : snapshot.FeedHealthReason
    };

    static IReadOnlyCollection<MarketOutlookComponentWrite> HydratedComponents(
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

    static IReadOnlyCollection<MarketOutlookComponentWrite> ItiComponents(
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
