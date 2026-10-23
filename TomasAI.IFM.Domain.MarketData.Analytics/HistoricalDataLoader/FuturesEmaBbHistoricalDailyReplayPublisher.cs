using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesBbSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Model.Processing;
using TomasAI.IFM.Domain.MarketData.Analytics.RegimeDiscovery;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesBbSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader;

/// <summary>Replays completed Daily sessions through the event-sourced EMA-to-Bollinger chain.</summary>
public sealed class FuturesEmaBbHistoricalDailyReplayPublisher(
    IActorService actorService,
    IMarketOutlookUpdateWriter updateWriter,
    IEventSourceActorDbContext? eventStore = null)
    : IHistoricalDailyReplayPublisher
{
    /// <inheritdoc />
    public async ValueTask<DateOnly?> RestoreAsync(MarketSeriesIdentity series, DateOnly targetValueDate,
        string targetContractId, DateOnly lastCompletedDate, CancellationToken cancellationToken)
    {
        if (eventStore is null || !IsEsSeries(series)) return null;
        var entity = new FuturesTradeSessionBarEntityId(series, TimeFrameType.Daily);
        var emaSubject = new ActorSubject(ActorType.Command, GenerateFuturesEmaSignalCommand.Actor,
            GenerateFuturesEmaSignalCommand.Verb, entity.Format());
        var bbSubject = new ActorSubject(ActorType.Command, GenerateFuturesBbSignalCommand.Actor,
            GenerateFuturesBbSignalCommand.Verb, entity.Format());
        var emaStream = await eventStore.GetEventStreamIdAsync(emaSubject.StreamId, cancellationToken).ConfigureAwait(false);
        var bbStream = await eventStore.GetEventStreamIdAsync(bbSubject.StreamId, cancellationToken).ConfigureAwait(false);
        var ema = new FuturesEmaSignalCommandState();
        var bb = new FuturesBbSignalCommandState();
        await eventStore.MapReduceActorEventStreamAsync<FuturesEmaSignalCommandState, FuturesEmaSignalGeneratedEvent>(
            emaStream, 1, ema.ReplayEvents, cancellationToken).ConfigureAwait(false);
        await eventStore.MapReduceActorEventStreamAsync<FuturesBbSignalCommandState, FuturesBbSignalGeneratedEvent>(
            bbStream, 1, bb.ReplayEvents, cancellationToken).ConfigureAwait(false);
        if (ema.FuturesEmaSignal is not { IsWarm: true } savedEma
            || bb.FuturesBbSignal is not { IsWarm: true } savedBb
            || ema.FuturesEmaCheckpoint is not { } emaCheckpoint || bb.FuturesBbCheckpoint is not { } bbCheckpoint
            || savedEma.Metadata.ValueDate > lastCompletedDate
            || savedEma.Metadata.TimeFrame != TimeFrameType.Daily || savedBb.Metadata.TimeFrame != TimeFrameType.Daily
            || savedEma.Metadata.ObservationId != savedBb.Metadata.ObservationId
            || emaCheckpoint.LastObservationId != bbCheckpoint.LastObservationId
            || emaCheckpoint.LastObservationId != savedEma.Metadata.ObservationId
            || savedEma.Metadata.SchemaVersion != 1 || savedBb.Metadata.SchemaVersion != 1
            || savedEma.Metadata.CalculationVersion != "ema-sma-seed-v1"
            || savedBb.Metadata.CalculationVersion != "bb-ema-center-population-v1"
            || savedEma.Metadata.MarketSeriesIdentity != series || savedBb.Metadata.MarketSeriesIdentity != series
            || !savedEma.Metadata.IsValid || !savedBb.Metadata.IsValid)
            return null;
        // A retry must not replace a newer baseline already built from stored observations.
        if (RegimeDiscoverySignalCacheAdapter.TryGetLatestEsDailyBaseline(targetContractId, out _, out _, out var current, out _)
            && current.Metadata.ContractId == targetContractId
            && current.Metadata.ValueDate > savedEma.Metadata.ValueDate
            && current.Metadata.ValueDate <= lastCompletedDate)
            return current.Metadata.ValueDate;
        PublishDailyBaseline(savedEma, savedBb, emaCheckpoint, bbCheckpoint,
            savedEma.Metadata.ContractId, targetValueDate, targetContractId);
        return savedEma.Metadata.ValueDate;
    }

    /// <inheritdoc />
    public async ValueTask PublishAsync(
        IReadOnlyList<FuturesEodObservationReadModel> observations,
        DateOnly targetValueDate,
        string targetContractId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(observations);
        var ordered = observations
            .Where(static value => value.IsComplete && value.IsValid)
            .OrderBy(static value => value.MarketSeriesIdentity.Format(), StringComparer.Ordinal)
            .ThenBy(static value => value.ValueDate)
            .ToArray();
        var baseline = CalculateDailyBaseline(ordered, cancellationToken);
        PublishDailyBaseline(baseline.Ema, baseline.Bb, baseline.EmaCheckpoint, baseline.BbCheckpoint,
            baseline.ContractId, targetValueDate, targetContractId);

        foreach (var source in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var observation = FuturesEodObservationMapper.ToDailyBar(source);
            var entityId = new FuturesTradeSessionBarEntityId(
                observation.MarketSeriesIdentity,
                TimeFrameType.Daily);
            var command = new GenerateFuturesEmaSignalCommand
            {
                CommandId = Guid.NewGuid(),
                Subject = new(
                    ActorType.Command,
                    GenerateFuturesEmaSignalCommand.Actor,
                    GenerateFuturesEmaSignalCommand.Verb,
                    entityId.Format()),
                EntityId = entityId,
                Observation = observation
            };
            var result = await actorService.RequestAsync<GenerateFuturesEmaSignalCommand,
                FuturesTradeSessionBarEntityId>(command).ConfigureAwait(false);
            if (!result.Success)
                throw new InvalidOperationException(
                    $"EMA historical replay was rejected for {source.ValueDate:O}: {result.ErrorMessage}");
        }
    }

    /// <summary>Calculates the latest ES daily EMA and Bollinger checkpoints from ordered complete historical sessions.</summary>
    static (FuturesEmaSignalReadModel? Ema, FuturesBbSignalReadModel? Bb, FuturesEmaAccumulatorCheckpoint? EmaCheckpoint,
        FuturesBbAccumulatorCheckpoint? BbCheckpoint, string ContractId) CalculateDailyBaseline(
        FuturesEodObservationReadModel[] ordered, CancellationToken cancellationToken)
    {
        FuturesEmaSignalReadModel? latestEsEma = null;
        FuturesBbSignalReadModel? latestEsBb = null;
        FuturesEmaAccumulatorCheckpoint? latestEsEmaCheckpoint = null;
        FuturesBbAccumulatorCheckpoint? latestEsBbCheckpoint = null;
        string latestEsContractId = string.Empty;
        foreach (var seriesGroup in ordered.GroupBy(static value => value.MarketSeriesIdentity))
        {
            FuturesEmaAccumulatorCheckpoint? emaCheckpoint = null;
            FuturesBbAccumulatorCheckpoint? bbCheckpoint = null;
            foreach (var source in seriesGroup.OrderBy(static value => value.ValueDate))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var observation = FuturesEodObservationMapper.ToDailyBar(source);
                var emaResult = FuturesEmaAccumulator.Apply(emaCheckpoint, observation);
                emaCheckpoint = emaResult.FuturesEmaCheckpoint;
                if (emaResult.FuturesEmaSignal is { } emaSignal)
                {
                    var bbResult = FuturesBbAccumulator.Apply(bbCheckpoint, observation, emaSignal);
                    bbCheckpoint = bbResult.FuturesBbCheckpoint;
                    if (IsEsSeries(seriesGroup.Key))
                    {
                        latestEsEma = emaSignal;
                        latestEsBb = bbResult.FuturesBbSignal;
                        latestEsEmaCheckpoint = emaCheckpoint;
                        latestEsBbCheckpoint = bbCheckpoint;
                        latestEsContractId = source.ContractId;
                    }
                }

            }
        }

        return (latestEsEma, latestEsBb, latestEsEmaCheckpoint, latestEsBbCheckpoint, latestEsContractId);
    }

    /// <summary>Publishes a warm daily calculation before durable actor reconciliation using the same target contract fallback.</summary>
    void PublishDailyBaseline(FuturesEmaSignalReadModel? latestEsEma, FuturesBbSignalReadModel? latestEsBb,
        FuturesEmaAccumulatorCheckpoint? latestEsEmaCheckpoint, FuturesBbAccumulatorCheckpoint? latestEsBbCheckpoint,
        string latestEsContractId, DateOnly targetValueDate, string targetContractId)
    {
        if (latestEsEma is { IsWarm: true }
            && latestEsBb is { IsWarm: true }
            && latestEsEmaCheckpoint is not null
            && latestEsBbCheckpoint is not null)
        {
            var resolvedTargetContractId = string.IsNullOrWhiteSpace(targetContractId)
                ? latestEsContractId
                : targetContractId;
            if (!string.IsNullOrWhiteSpace(resolvedTargetContractId))
            {
                var outlookEntityId = new MarketOutlookEntityId(resolvedTargetContractId, targetValueDate);
                // Publish the calculation before durable actor reconciliation. Stored EOD data is
                // sufficient to make the process-local Daily baseline available immediately.
                RegimeDiscoverySignalCacheAdapter.PublishDailyBaseline(
                    resolvedTargetContractId,
                    latestEsEma,
                    latestEsEmaCheckpoint,
                    latestEsBb,
                    latestEsBbCheckpoint);
                updateWriter.Submit(new HistoricalWarmupMarketOutlookUpdate
                {
                    UpdateId = latestEsEma.Metadata.ObservationId.Value,
                    EntityId = outlookEntityId,
                    ReceivedAtUtc = DateTime.UtcNow,
                    MarketDataAsOfUtc = latestEsEma.Metadata.MarketDataAsOfUtc.UtcDateTime,
                    CommandId = latestEsEma.Metadata.ObservationId.Value,
                    AggregateId = outlookEntityId.Format(),
                    EventSource = nameof(FuturesEmaBbHistoricalDailyReplayPublisher),
                    SourceSequence = latestEsEma.Metadata.SourceSequence,
                    Ema = latestEsEma,
                    BollingerBand = latestEsBb
                });
            }
        }

    }

    static bool IsEsSeries(MarketSeriesIdentity series) =>
        series.FuturesSeriesId is { } continuation
            ? string.Equals(continuation.RootSymbol, "ES", StringComparison.OrdinalIgnoreCase)
            : series.Kind == MarketSeriesIdentityKind.Contract
              && series.ContractId.StartsWith("ES", StringComparison.OrdinalIgnoreCase);


}
