using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesBbSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader;

/// <summary>Replays completed Daily sessions through the event-sourced EMA-to-Bollinger chain.</summary>
public sealed class FuturesEmaBbHistoricalDailyReplayPublisher(IActorService actorService)
    : IHistoricalDailyReplayPublisher
{
    /// <inheritdoc />
    public async ValueTask PublishAsync(
        IReadOnlyList<FuturesEodObservationReadModel> observations,
        DateOnly targetValueDate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(observations);
        var ordered = observations
            .Where(static value => value.IsComplete && value.IsValid)
            .OrderBy(static value => value.MarketSeriesIdentity.Format(), StringComparer.Ordinal)
            .ThenBy(static value => value.ValueDate)
            .ToArray();
        FuturesEmaSignalReadModel? latestEsEma = null;
        FuturesBbSignalReadModel? latestEsBb = null;
        string latestEsContractId = string.Empty;
        foreach (var seriesGroup in ordered.GroupBy(static value => value.MarketSeriesIdentity))
        {
            FuturesEmaAccumulatorCheckpoint? emaCheckpoint = null;
            FuturesBbAccumulatorCheckpoint? bbCheckpoint = null;
            foreach (var source in seriesGroup.OrderBy(static value => value.ValueDate))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var observation = ToDailyBar(source);
                var emaResult = FuturesEmaAccumulator.Apply(emaCheckpoint, observation);
                emaCheckpoint = emaResult.Checkpoint;
                if (emaResult.Signal is { } emaSignal)
                {
                    var bbResult = FuturesBbAccumulator.Apply(bbCheckpoint, observation, emaSignal);
                    bbCheckpoint = bbResult.Checkpoint;
                    if (IsEsSeries(seriesGroup.Key))
                    {
                        latestEsEma = emaSignal;
                        latestEsBb = bbResult.Signal;
                        latestEsContractId = source.ContractId;
                    }
                }

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

        if (latestEsEma is not { IsWarm: true }
            || latestEsBb is not { IsWarm: true }
            || string.IsNullOrWhiteSpace(latestEsContractId))
            return;

        var outlookEntityId = new MarketOutlookEntityId(latestEsContractId, targetValueDate);
        var sourceEventId = latestEsEma.Metadata.ObservationId.Value;
        var reconcile = new ObserveMarketOutlookComponentCommand(
            outlookEntityId,
            sourceEventId,
            latestEsEma.Metadata.SourceSequence,
            latestEsEma.Metadata.MarketDataAsOfUtc.UtcDateTime,
            "HistoricalAnalyticsWarmup",
            futuresEmaSignal: latestEsEma,
            futuresBbSignal: latestEsBb)
        {
            CommandId = Guid.NewGuid(),
            Subject = new(
                ActorType.Command,
                ObserveMarketOutlookComponentCommand.Actor,
                ObserveMarketOutlookComponentCommand.Verb,
                outlookEntityId.Format())
        };
        var reconcileResult = await actorService.RequestAsync<ObserveMarketOutlookComponentCommand,
            MarketOutlookEntityId>(reconcile).ConfigureAwait(false);
        if (!reconcileResult.Success)
            throw new InvalidOperationException(
                $"Market Outlook historical Analytics reconciliation was rejected: {reconcileResult.ErrorMessage}");
    }

    static bool IsEsSeries(MarketSeriesIdentity series) =>
        series.FuturesSeriesId is { } continuation
            ? string.Equals(continuation.RootSymbol, "ES", StringComparison.OrdinalIgnoreCase)
            : series.Kind == MarketSeriesIdentityKind.Contract
              && series.ContractId.StartsWith("ES", StringComparison.OrdinalIgnoreCase);

    static FuturesTradeSessionBarReadModel ToDailyBar(FuturesEodObservationReadModel source) => new()
    {
        MarketSeriesIdentity = source.MarketSeriesIdentity,
        ObservationId = source.ObservationId,
        ContractId = source.ContractId,
        ValueDate = source.ValueDate,
        TimeFrame = TimeFrameType.Daily,
        IntervalStartUtc = source.SessionStartUtc,
        IntervalEndUtc = source.SessionEndUtc,
        Open = source.Open,
        High = source.High,
        Low = source.Low,
        Close = source.Close,
        Volume = source.Volume,
        TradeCount = source.TradeCount,
        PriceVolumeSum = source.PriceVolumeSum,
        FirstSourceSequence = source.FirstSourceSequence,
        LastSourceSequence = source.LastSourceSequence,
        FirstMarketEventUtc = source.FirstMarketEventUtc,
        LastMarketEventUtc = source.LastMarketEventUtc,
        CalculatedAtUtc = source.SessionEndUtc > source.LastMarketEventUtc
            ? source.SessionEndUtc
            : source.LastMarketEventUtc,
        SchemaVersion = source.SchemaVersion,
        CalculationVersion = "historical-daily-v1",
        IsComplete = source.IsComplete,
        IsValid = source.IsValid,
        ValidationIssues = [],
        CalculationMethod = MarketSignalCalculationMethod.NormalizedHistoricalAggregate
    };
}
