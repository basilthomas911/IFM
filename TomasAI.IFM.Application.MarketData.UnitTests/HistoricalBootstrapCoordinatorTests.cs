using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Databento.Historical;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Observation;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class HistoricalBootstrapCoordinatorTests
{
    [Fact]
    public async Task OneYearFixtureCreatesAtLeast252IdempotentSessionsAndReusesCompletedRequest()
    {
        var calendar = new CmeFuturesMarketSessionCalendar();
        var series = MarketSeriesIdentity.ForFuturesSeries(
            new FuturesSeriesId("ES", "calendar-front", "unadjusted", 1));
        var request = new MarketDataHistoricalRequest
        {
            BootstrapAttemptId = Guid.NewGuid(),
            Series = [new() { SeriesIdentity = series, Schema = TomasAI.IFM.Framework.MarketData.Contracts.Historical.HistoricalDataSchema.OhlcvOneMinute }],
            StartDate = new(2024, 1, 2), EndDate = new(2024, 12, 31),
            MaximumCostUsd = 1, MaximumBytes = 10_000_000,
            NormalizationVersion = "fixture-v1", RequestedBy = "test"
        };
        var api = new FixtureHistoricalApi(calendar);
        var states = new MemoryBootstrapStore();
        var observations = new MemoryObservationStore();
        var replay = new RecordingReplayPublisher();
        var coordinator = new HistoricalBootstrapCoordinator(
            api, states, observations, replay, calendar, TimeProvider.System);

        var first = await coordinator.ExecuteAsync(request, CancellationToken.None);
        var second = await coordinator.ExecuteAsync(
            request with { BootstrapAttemptId = Guid.NewGuid() }, CancellationToken.None);

        Assert.Equal(HistoricalBootstrapStatus.Completed, first.Status);
        Assert.Same(first, second);
        Assert.True(first.Audit!.ValidSessionCount >= 252);
        Assert.Empty(first.Audit.Gaps);
        Assert.Equal(first.Audit.ValidSessionCount, observations.Raw.Count);
        Assert.Equal(1, api.AcquireCount);
        Assert.Equal(first.Audit.ValidSessionCount, replay.BatchCount);
    }

    sealed class FixtureHistoricalApi(IMarketSessionCalendar calendar) : IMarketDataHistoricalApi
    {
        internal int AcquireCount { get; private set; }

        public ValueTask<MarketDataHistoricalEstimate> EstimateAsync(
            MarketDataHistoricalRequest request,
            CancellationToken cancellationToken) => ValueTask.FromResult(new MarketDataHistoricalEstimate(
                request.BootstrapAttemptId, 0, 1_000, 252, "ONE-YEAR-FIXTURE", DateTimeOffset.UtcNow));

        public async ValueTask<MarketDataHistoricalManifest> AcquireAsync(
            MarketDataHistoricalRequest request,
            HistoricalAcquisitionCheckpoint checkpoint,
            IHistoricalObservationSink sink,
            CancellationToken cancellationToken)
        {
            AcquireCount++;
            long ordinal = 0;
            foreach (var series in request.Series)
            for (var date = request.StartDate; date <= request.EndDate; date = date.AddDays(1))
            {
                if (!calendar.IsTradingDate(date)) continue;
                var bounds = calendar.GetSession(date);
                var end = bounds.StartUtc.AddMinutes(1);
                var observation = new FuturesTradeSessionBarReadModel
                {
                    MarketSeriesIdentity = series.SeriesIdentity,
                    ObservationId = FuturesTradeSessionBarId.Create(
                        series.SeriesIdentity, TimeFrameType.OneMinute, end, ordinal),
                    ContractId = date < new DateOnly(2024, 6, 14) ? "ESM4" : "ESU4",
                    ValueDate = date, TimeFrame = TimeFrameType.OneMinute,
                    IntervalStartUtc = bounds.StartUtc, IntervalEndUtc = end,
                    Open = 5000, High = 5002, Low = 4998, Close = 5001,
                    Volume = 10, TradeCount = 1, PriceVolumeSum = 50_010,
                    FirstSourceSequence = ordinal, LastSourceSequence = ordinal,
                    FirstMarketEventUtc = bounds.StartUtc, LastMarketEventUtc = bounds.StartUtc,
                    CalculatedAtUtc = DateTimeOffset.UtcNow, CalculationVersion = "fixture-v1",
                    IsComplete = true, IsValid = true,
                    CalculationMethod = MarketSignalCalculationMethod.NormalizedHistoricalAggregate
                };
                await sink.AcceptAsync(new(
                    request.BootstrapAttemptId, "fixture", ordinal, ordinal.ToString(),
                    [observation], [], $"{ordinal:X64}", true), cancellationToken);
                ordinal++;
            }
            return new MarketDataHistoricalManifest
            {
                ManifestId = Guid.NewGuid(), BootstrapAttemptId = request.BootstrapAttemptId,
                ProviderJobId = "fixture-job", RequestSha256 = "ONE-YEAR-FIXTURE",
                NormalizedSha256 = "FIXTURE", ObservationCount = ordinal,
                FirstValueDate = request.StartDate, LastValueDate = request.EndDate,
                CompletedAtUtc = DateTimeOffset.UtcNow
            };
        }
    }

    sealed class MemoryBootstrapStore : IHistoricalBootstrapStore
    {
        readonly Dictionary<Guid, HistoricalBootstrapState> states = [];
        public ValueTask<HistoricalBootstrapState?> GetAsync(Guid attemptId, CancellationToken cancellationToken) =>
            ValueTask.FromResult(states.GetValueOrDefault(attemptId));
        public ValueTask<HistoricalBootstrapState?> GetCompletedByRequestHashAsync(string hash, CancellationToken cancellationToken) =>
            ValueTask.FromResult(states.Values.FirstOrDefault(x => x.RequestSha256 == hash && x.Status == HistoricalBootstrapStatus.Completed));
        public ValueTask SaveAsync(HistoricalBootstrapState state, CancellationToken cancellationToken)
        {
            states[state.BootstrapAttemptId] = state;
            return ValueTask.CompletedTask;
        }
    }

    sealed class MemoryObservationStore : IHistoricalObservationStore
    {
        internal Dictionary<Guid, FuturesTradeSessionBarReadModel> Observations { get; } = [];
        internal Dictionary<string, FuturesEodObservationReadModel> Raw { get; } = [];
        public ValueTask<bool> TryWriteObservationAsync(FuturesTradeSessionBarReadModel value, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Observations.TryAdd(value.ObservationId.Value, value));
        public ValueTask<bool> TryWriteRawEodAsync(FuturesEodObservationReadModel value, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Raw.TryAdd($"{value.MarketSeriesIdentity.Format()}|{value.ValueDate:O}", value));
        public ValueTask<FuturesEodObservationReadModel?> GetRawEodAsync(MarketSeriesIdentity series, DateOnly date, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Raw.GetValueOrDefault($"{series.Format()}|{date:O}"));
    }

    sealed class RecordingReplayPublisher : IHistoricalReplayPublisher
    {
        internal int BatchCount { get; private set; }
        public ValueTask PublishAsync(NormalizedHistoricalBatch batch, CancellationToken cancellationToken)
        {
            BatchCount++;
            return ValueTask.CompletedTask;
        }
    }
}
