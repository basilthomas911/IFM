using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Observation;

namespace TomasAI.IFM.Application.MarketData.Historical;

/// <summary>
/// Coordinates one resumable, roll-aware historical acquisition without retaining provider records in actor state.
/// </summary>
public sealed class HistoricalDataLoader
{
    private readonly IMarketDataHistoricalApi historicalApi;
    private readonly IHistoricalDataLoaderStore dataLoaderStore;
    private readonly IHistoricalObservationStore observationStore;
    private readonly IHistoricalReplayPublisher replayPublisher;
    private readonly IMarketSessionCalendar calendar;
    private readonly TimeProvider timeProvider;

    /// <summary>Initializes the data load coordinator.</summary>
    public HistoricalDataLoader(
        IMarketDataHistoricalApi historicalApi,
        IHistoricalDataLoaderStore dataLoaderStore,
        IHistoricalObservationStore observationStore,
        IHistoricalReplayPublisher replayPublisher,
        IMarketSessionCalendar calendar,
        TimeProvider timeProvider)
    {
        this.historicalApi = historicalApi ?? throw new ArgumentNullException(nameof(historicalApi));
        this.dataLoaderStore = dataLoaderStore ?? throw new ArgumentNullException(nameof(dataLoaderStore));
        this.observationStore = observationStore ?? throw new ArgumentNullException(nameof(observationStore));
        this.replayPublisher = replayPublisher ?? throw new ArgumentNullException(nameof(replayPublisher));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>Executes or resumes one data load and reuses an immutable completed request.</summary>
    public async ValueTask<HistoricalDataLoaderState> ExecuteAsync(
        MarketDataHistoricalRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var estimate = await historicalApi.EstimateAsync(request, cancellationToken).ConfigureAwait(false);
        if (await dataLoaderStore.GetCompletedByRequestHashAsync(
                estimate.RequestSha256, cancellationToken).ConfigureAwait(false) is { } completed)
        {
            return completed;
        }

        var existing = await dataLoaderStore.GetAsync(
            request.DataLoadAttemptId, cancellationToken).ConfigureAwait(false);
        var checkpoint = existing?.Checkpoint ?? new HistoricalAcquisitionCheckpoint
        {
            DataLoadAttemptId = request.DataLoadAttemptId,
            Stage = HistoricalAcquisitionStage.Estimated
        };
        var state = new HistoricalDataLoaderState
        {
            DataLoadAttemptId = request.DataLoadAttemptId,
            RequestSha256 = estimate.RequestSha256,
            Status = HistoricalDataLoaderStatus.Processing,
            Checkpoint = checkpoint,
            UpdatedAtUtc = timeProvider.GetUtcNow()
        };
        await dataLoaderStore.SaveAsync(state, cancellationToken).ConfigureAwait(false);

        var sink = new DataLoadSink(
            request, observationStore, replayPublisher, calendar, dataLoaderStore, state, timeProvider);
        try
        {
            var manifest = await historicalApi.AcquireAsync(
                request, checkpoint, sink, cancellationToken).ConfigureAwait(false);
            var audit = await sink.CompleteAsync(cancellationToken).ConfigureAwait(false);
            state = state with
            {
                Status = HistoricalDataLoaderStatus.Completed,
                Checkpoint = state.Checkpoint with { Stage = HistoricalAcquisitionStage.Completed },
                Manifest = manifest,
                Audit = audit,
                UpdatedAtUtc = timeProvider.GetUtcNow()
            };
            await dataLoaderStore.SaveAsync(state, cancellationToken).ConfigureAwait(false);
            return state;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            state = state with
            {
                Status = HistoricalDataLoaderStatus.Failed,
                Checkpoint = sink.Checkpoint,
                ErrorMessage = exception.Message,
                UpdatedAtUtc = timeProvider.GetUtcNow()
            };
            await dataLoaderStore.SaveAsync(state, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    sealed class DataLoadSink : IHistoricalObservationSink, IHistoricalAcquisitionCheckpointSink
    {
        private readonly MarketDataHistoricalRequest request;
        private readonly IHistoricalObservationStore store;
        private readonly IHistoricalReplayPublisher publisher;
        private readonly IMarketSessionCalendar calendar;
        private readonly IHistoricalDataLoaderStore dataLoaderStore;
        private readonly HistoricalDataLoaderState initialState;
        private readonly TimeProvider timeProvider;
        private readonly Dictionary<SessionKey, SessionAccumulator> sessions = [];

        internal DataLoadSink(
            MarketDataHistoricalRequest request,
            IHistoricalObservationStore store,
            IHistoricalReplayPublisher publisher,
            IMarketSessionCalendar calendar,
            IHistoricalDataLoaderStore dataLoaderStore,
            HistoricalDataLoaderState initialState,
            TimeProvider timeProvider)
        {
            this.request = request;
            this.store = store;
            this.publisher = publisher;
            this.calendar = calendar;
            this.dataLoaderStore = dataLoaderStore;
            this.initialState = initialState;
            this.timeProvider = timeProvider;
            Checkpoint = initialState.Checkpoint;
        }

        internal HistoricalAcquisitionCheckpoint Checkpoint { get; private set; }

        public async ValueTask CheckpointAsync(
            HistoricalAcquisitionCheckpoint checkpoint,
            CancellationToken cancellationToken)
        {
            if (checkpoint.DataLoadAttemptId != request.DataLoadAttemptId)
                throw new InvalidDataException("Acquisition checkpoint does not belong to the active data load attempt.");
            Checkpoint = checkpoint;
            await dataLoaderStore.SaveAsync(initialState with
            {
                Checkpoint = Checkpoint,
                UpdatedAtUtc = timeProvider.GetUtcNow()
            }, cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask AcceptAsync(NormalizedHistoricalBatch batch, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(batch);
            if (batch.DataLoadAttemptId != request.DataLoadAttemptId)
                throw new InvalidDataException("Replay batch does not belong to the active data load attempt.");
            foreach (var observation in batch.Observations)
            {
                await store.TryWriteObservationAsync(observation, cancellationToken).ConfigureAwait(false);
                var key = new SessionKey(
                    observation.MarketSeriesIdentity, observation.ContractId, observation.ValueDate);
                if (!sessions.TryGetValue(key, out var accumulator))
                {
                    sessions.Add(key, accumulator = new SessionAccumulator(key, calendar.GetSession(key.ValueDate)));
                }
                accumulator.Add(observation);
            }
            await publisher.PublishAsync(batch, cancellationToken).ConfigureAwait(false);
            Checkpoint = new HistoricalAcquisitionCheckpoint
            {
                DataLoadAttemptId = request.DataLoadAttemptId,
                Stage = HistoricalAcquisitionStage.Normalized,
                ProviderJobId = Checkpoint.ProviderJobId,
                ProviderFileId = batch.ProviderFileId,
                BatchOrdinal = batch.BatchOrdinal,
                SourcePosition = batch.SourcePosition
            };
            await dataLoaderStore.SaveAsync(initialState with
            {
                Checkpoint = Checkpoint,
                UpdatedAtUtc = timeProvider.GetUtcNow()
            }, cancellationToken).ConfigureAwait(false);
        }

        internal async ValueTask<HistoricalDataLoaderAudit> CompleteAsync(CancellationToken cancellationToken)
        {
            foreach (var accumulator in sessions.Values)
            {
                await store.TryWriteRawEodAsync(accumulator.Build(), cancellationToken).ConfigureAwait(false);
            }
            var gaps = new List<HistoricalDataLoaderGap>();
            foreach (var series in request.Series)
            {
                for (var date = request.StartDate; date <= request.EndDate; date = date.AddDays(1))
                {
                    if (!calendar.IsTradingDate(date)) continue;
                    if (!sessions.Keys.Any(x => x.SeriesIdentity == series.SeriesIdentity && x.ValueDate == date))
                    {
                        gaps.Add(new(date, series.SeriesIdentity.Format(), "MISSING_TRADING_SESSION"));
                    }
                }
            }
            var rolls = sessions.Keys
                .GroupBy(x => x.SeriesIdentity)
                .SelectMany(group =>
                {
                    var ordered = group.OrderBy(x => x.ValueDate).ThenBy(x => x.ContractId, StringComparer.Ordinal).ToArray();
                    var values = new List<HistoricalDataLoaderRoll>();
                    for (var index = 1; index < ordered.Length; index++)
                    {
                        if (!string.Equals(ordered[index - 1].ContractId, ordered[index].ContractId, StringComparison.Ordinal))
                        {
                            values.Add(new(group.Key.Format(), ordered[index].ValueDate,
                                ordered[index - 1].ContractId, ordered[index].ContractId));
                        }
                    }
                    return values;
                })
                .ToArray();
            return new(sessions.Count, gaps, rolls);
        }
    }

    readonly record struct SessionKey(
        MarketSeriesIdentity SeriesIdentity,
        string ContractId,
        DateOnly ValueDate);

    sealed class SessionAccumulator(SessionKey key, MarketSessionBounds bounds)
    {
        private decimal open;
        private decimal high = decimal.MinValue;
        private decimal low = decimal.MaxValue;
        private decimal close;
        private decimal volume;
        private long tradeCount;
        private decimal priceVolumeSum;
        private long firstSequence = long.MaxValue;
        private long lastSequence;
        private DateTimeOffset firstEvent = DateTimeOffset.MaxValue;
        private DateTimeOffset lastEvent = DateTimeOffset.MinValue;
        private bool hasValue;

        internal void Add(FuturesTradeSessionBarReadModel value)
        {
            if (!hasValue || value.FirstMarketEventUtc < firstEvent)
            {
                open = value.Open;
                firstEvent = value.FirstMarketEventUtc;
            }
            if (!hasValue || value.LastMarketEventUtc >= lastEvent)
            {
                close = value.Close;
                lastEvent = value.LastMarketEventUtc;
            }
            high = Math.Max(high, value.High);
            low = Math.Min(low, value.Low);
            volume += value.Volume;
            tradeCount += value.TradeCount;
            priceVolumeSum += value.PriceVolumeSum;
            firstSequence = Math.Min(firstSequence, value.FirstSourceSequence);
            lastSequence = Math.Max(lastSequence, value.LastSourceSequence);
            hasValue = true;
        }

        internal FuturesEodObservationReadModel Build()
        {
            if (!hasValue) throw new InvalidOperationException("Cannot build an empty session.");
            var id = FuturesTradeSessionBarId.Create(
                key.SeriesIdentity, TimeFrameType.Daily, bounds.EndUtc, lastSequence);
            return new FuturesEodObservationReadModel
            {
                MarketSeriesIdentity = key.SeriesIdentity,
                ContractId = key.ContractId,
                ValueDate = key.ValueDate,
                SessionStartUtc = bounds.StartUtc,
                SessionEndUtc = bounds.EndUtc,
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = volume,
                TradeCount = tradeCount,
                PriceVolumeSum = priceVolumeSum,
                ObservationId = id,
                FirstSourceSequence = firstSequence,
                LastSourceSequence = lastSequence,
                FirstMarketEventUtc = firstEvent,
                LastMarketEventUtc = lastEvent,
                IsComplete = true,
                IsValid = true
            };
        }
    }
}
