using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Databento.Historical;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Framework.MarketData.Contracts.Historical;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class HistoricalAnalyticsWarmupServiceTests
{
    static readonly MarketSeriesIdentity Es = MarketSeriesIdentity.ForFuturesSeries(
        new FuturesSeriesId("ES", "calendar-front", "unadjusted", 1));

    [Fact]
    public async Task ProductionIgnoresRequestBeforeStorageOrProviderAccess()
    {
        var fixture = new Fixture(isDevelopment: false, enabled: true, seedCoverage: false);

        var result = await fixture.Service.EnsureAsync(fixture.Request, CancellationToken.None);

        Assert.Equal(HistoricalAnalyticsWarmupOutcome.IgnoredInProduction, result.Outcome);
        Assert.Equal(0, fixture.ObservationStore.RangeReads);
        Assert.Equal(0, fixture.Api.AcquireCount);
        Assert.Equal(0, fixture.DailyReplay.PublishCount);
    }

    [Fact]
    public async Task CompleteStoredYearReplaysOnceAndRepeatedStartupIsAlreadyCurrent()
    {
        var fixture = new Fixture(isDevelopment: true, enabled: true, seedCoverage: true);

        var first = await fixture.Service.EnsureAsync(fixture.Request, CancellationToken.None);
        var second = await fixture.Service.EnsureAsync(
            fixture.Request with { DataLoadAttemptId = Guid.NewGuid() }, CancellationToken.None);

        Assert.Equal(HistoricalAnalyticsWarmupOutcome.ReplayedFromStorage, first.Outcome);
        Assert.Equal(HistoricalAnalyticsWarmupOutcome.AlreadyCurrent, second.Outcome);
        Assert.True(first.ValidSessionCount >= 201);
        Assert.Equal(0, fixture.Api.AcquireCount);
        Assert.Equal(1, fixture.DailyReplay.PublishCount);
        Assert.Equal(first.ValidSessionCount, fixture.DailyReplay.LastObservations.Count);
        Assert.Equal("ES-ACTIVE", fixture.DailyReplay.LastTargetContractId);
    }

    [Fact]
    public async Task ConcurrentSameDayStartsReplayOnceWithoutProviderAcquisition()
    {
        var fixture = new Fixture(isDevelopment: true, enabled: true, seedCoverage: true);

        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ =>
            fixture.Service.EnsureAsync(
                fixture.Request with { DataLoadAttemptId = Guid.NewGuid() },
                CancellationToken.None).AsTask()));

        Assert.Single(results, value => value.Outcome == HistoricalAnalyticsWarmupOutcome.ReplayedFromStorage);
        Assert.Equal(9, results.Count(value => value.Outcome == HistoricalAnalyticsWarmupOutcome.AlreadyCurrent));
        Assert.Equal(0, fixture.Api.AcquireCount);
        Assert.Equal(1, fixture.DailyReplay.PublishCount);
    }

    [Fact]
    public async Task OneTrailingUnpublishedSessionReplaysQualifiedHistoryWithoutProviderAcquisition()
    {
        var fixture = new Fixture(isDevelopment: true, enabled: true, seedCoverage: true);
        var trailingDate = fixture.ObservationStore.Raw.Max(static value => value.ValueDate);
        fixture.ObservationStore.Raw.RemoveAll(value => value.ValueDate == trailingDate);

        var result = await fixture.Service.EnsureAsync(fixture.Request, CancellationToken.None);

        Assert.Equal(HistoricalAnalyticsWarmupOutcome.ReplayedFromStorage, result.Outcome);
        Assert.Equal(1, result.MissingSessionCount);
        Assert.True(result.ValidSessionCount >= 201);
        Assert.Equal(0, fixture.Api.AcquireCount);
        Assert.Equal(1, fixture.DailyReplay.PublishCount);
    }

    [Fact]
    public async Task SeparateMissingTradingDateGroupsAcquireOnlyThoseRanges()
    {
        var calendar = new CmeFuturesMarketSessionCalendar();
        var store = new MemoryObservationStore();
        var end = new DateOnly(2024, 12, 31);
        var start = end.AddDays(-364);
        var tradingDates = Enumerable.Range(0, 365)
            .Select(offset => start.AddDays(offset))
            .Where(calendar.IsTradingDate)
            .ToArray();
        var missing = new[] { tradingDates[40], tradingDates[120] };
        long sequence = 1;
        foreach (var date in tradingDates.Except(missing))
            store.Raw.Add(Session(calendar, date, sequence++));
        var api = new FillingHistoricalApi(calendar);
        var loader = new HistoricalDataLoader(
            api,
            new MemoryDataLoaderStore(),
            store,
            new NullHistoricalReplayPublisher(),
            calendar,
            TimeProvider.System);
        var replay = new RecordingDailyReplayPublisher();
        var service = new HistoricalAnalyticsWarmupService(
            new HistoricalAnalyticsWarmupOptions
            {
                Enabled = true,
                IsDevelopmentEnvironment = true
            },
            loader,
            store,
            replay,
            calendar,
            TimeProvider.System);
        var request = new MarketDataHistoricalRequest
        {
            DataLoadAttemptId = Guid.NewGuid(),
            Series = [new() { SeriesIdentity = Es, Schema = HistoricalDataSchema.OhlcvOneMinute }],
            StartDate = start,
            EndDate = end,
            MaximumCostUsd = 10,
            MaximumBytes = 1_073_741_824,
            NormalizationVersion = "historical-daily-v1",
            RequestedBy = "test",
            AnalyticsTargetContractId = "ES-ACTIVE"
        };

        var result = await service.EnsureAsync(request, CancellationToken.None);

        Assert.Equal(HistoricalAnalyticsWarmupOutcome.AcquiredAndReplayed, result.Outcome);
        Assert.Equal(2, api.Requests.Count);
        Assert.All(api.Requests, value => Assert.Equal(value.StartDate, value.EndDate));
        Assert.Equal(missing.Order(), api.Requests.Select(value => value.StartDate).Order());
        Assert.Equal(tradingDates.Length, result.ValidSessionCount);
        Assert.Equal(1, replay.PublishCount);
    }

    [Fact]
    public void OptionsRequireOneYearAndEma200WarmupDepth()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new HistoricalAnalyticsWarmupOptions
        {
            Enabled = true,
            IsDevelopmentEnvironment = true,
            LookbackCalendarDays = 364
        }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new HistoricalAnalyticsWarmupOptions
        {
            Enabled = true,
            IsDevelopmentEnvironment = true,
            MinimumValidDailySessions = 200
        }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new HistoricalAnalyticsWarmupOptions
        {
            Enabled = true,
            IsDevelopmentEnvironment = true,
            TrailingProviderAvailabilityGraceSessions = 6
        }.Validate());
    }

    sealed class Fixture
    {
        internal Fixture(bool isDevelopment, bool enabled, bool seedCoverage)
        {
            Calendar = new CmeFuturesMarketSessionCalendar();
            ObservationStore = new MemoryObservationStore();
            var end = new DateOnly(2024, 12, 31);
            var start = end.AddDays(-364);
            if (seedCoverage)
            {
                long sequence = 1;
                for (var date = start; date <= end; date = date.AddDays(1))
                    if (Calendar.IsTradingDate(date))
                        ObservationStore.Raw.Add(Session(Calendar, date, sequence++));
            }
            Api = new NeverAcquireApi();
            var loader = new HistoricalDataLoader(
                Api,
                new MemoryDataLoaderStore(),
                ObservationStore,
                new NullHistoricalReplayPublisher(),
                Calendar,
                TimeProvider.System);
            DailyReplay = new RecordingDailyReplayPublisher();
            Service = new HistoricalAnalyticsWarmupService(
                new HistoricalAnalyticsWarmupOptions
                {
                    Enabled = enabled,
                    IsDevelopmentEnvironment = isDevelopment
                },
                loader,
                ObservationStore,
                DailyReplay,
                Calendar,
                TimeProvider.System);
            Request = new MarketDataHistoricalRequest
            {
                DataLoadAttemptId = Guid.NewGuid(),
                Series = [new() { SeriesIdentity = Es, Schema = HistoricalDataSchema.OhlcvOneMinute }],
                StartDate = start,
                EndDate = end,
                MaximumCostUsd = 10,
                MaximumBytes = 1_073_741_824,
                NormalizationVersion = "historical-daily-v1",
                RequestedBy = "test",
                AnalyticsTargetContractId = "ES-ACTIVE"
            };
        }

        internal CmeFuturesMarketSessionCalendar Calendar { get; }
        internal MemoryObservationStore ObservationStore { get; }
        internal NeverAcquireApi Api { get; }
        internal RecordingDailyReplayPublisher DailyReplay { get; }
        internal HistoricalAnalyticsWarmupService Service { get; }
        internal MarketDataHistoricalRequest Request { get; }
    }

    static FuturesEodObservationReadModel Session(IMarketSessionCalendar calendar, DateOnly date, long sequence)
    {
        var bounds = calendar.GetSession(date);
        return new()
        {
            MarketSeriesIdentity = Es,
            ContractId = "ESZ24",
            ValueDate = date,
            SessionStartUtc = bounds.StartUtc,
            SessionEndUtc = bounds.EndUtc,
            Open = 5000,
            High = 5010,
            Low = 4990,
            Close = 5005,
            Volume = 100,
            TradeCount = 10,
            PriceVolumeSum = 500_500,
            ObservationId = FuturesTradeSessionBarId.Create(Es, TimeFrameType.Daily, bounds.EndUtc, sequence),
            FirstSourceSequence = sequence,
            LastSourceSequence = sequence,
            FirstMarketEventUtc = bounds.StartUtc,
            LastMarketEventUtc = bounds.EndUtc.AddTicks(-1),
            IsComplete = true,
            IsValid = true
        };
    }

    sealed class MemoryObservationStore : IHistoricalObservationStore
    {
        internal List<FuturesEodObservationReadModel> Raw { get; } = [];
        internal int RangeReads { get; private set; }
        public ValueTask<bool> TryWriteObservationAsync(FuturesTradeSessionBarReadModel observation, CancellationToken token)
            => ValueTask.FromResult(true);
        public ValueTask<bool> TryWriteRawEodAsync(FuturesEodObservationReadModel observation, CancellationToken token)
        {
            Raw.Add(observation);
            return ValueTask.FromResult(true);
        }
        public ValueTask<FuturesEodObservationReadModel?> GetRawEodAsync(MarketSeriesIdentity series, DateOnly date, CancellationToken token)
            => ValueTask.FromResult(Raw.FirstOrDefault(value => value.MarketSeriesIdentity == series && value.ValueDate == date));
        public ValueTask<IReadOnlyList<FuturesEodObservationReadModel>> GetRawEodRangeAsync(
            MarketSeriesIdentity series, DateOnly start, DateOnly end, CancellationToken token)
        {
            RangeReads++;
            return ValueTask.FromResult<IReadOnlyList<FuturesEodObservationReadModel>>(
                Raw.Where(value => value.MarketSeriesIdentity == series && value.ValueDate >= start && value.ValueDate <= end).ToArray());
        }
    }

    sealed class RecordingDailyReplayPublisher : IHistoricalDailyReplayPublisher
    {
        internal int PublishCount { get; private set; }
        internal IReadOnlyList<FuturesEodObservationReadModel> LastObservations { get; private set; } = [];
        internal string LastTargetContractId { get; private set; } = string.Empty;
        public ValueTask PublishAsync(
            IReadOnlyList<FuturesEodObservationReadModel> observations,
            DateOnly targetValueDate,
            string targetContractId,
            CancellationToken token)
        {
            PublishCount++;
            LastObservations = observations;
            LastTargetContractId = targetContractId;
            return ValueTask.CompletedTask;
        }
    }

    sealed class NeverAcquireApi : IMarketDataHistoricalApi
    {
        internal int AcquireCount { get; private set; }
        public ValueTask<MarketDataHistoricalEstimate> EstimateAsync(MarketDataHistoricalRequest request, CancellationToken token)
            => throw new InvalidOperationException("Provider access was not expected.");
        public ValueTask<MarketDataHistoricalManifest> AcquireAsync(
            MarketDataHistoricalRequest request, HistoricalAcquisitionCheckpoint checkpoint,
            IHistoricalObservationSink sink, CancellationToken token)
        {
            AcquireCount++;
            throw new InvalidOperationException("Provider access was not expected.");
        }
    }

    sealed class FillingHistoricalApi(IMarketSessionCalendar calendar) : IMarketDataHistoricalApi
    {
        internal List<MarketDataHistoricalRequest> Requests { get; } = [];

        public ValueTask<MarketDataHistoricalEstimate> EstimateAsync(
            MarketDataHistoricalRequest request,
            CancellationToken token) => ValueTask.FromResult(new MarketDataHistoricalEstimate(
                request.DataLoadAttemptId,
                0,
                1_000,
                1,
                $"{request.Series[0].SeriesIdentity.Format()}|{request.StartDate:O}|{request.EndDate:O}",
                DateTimeOffset.UtcNow));

        public async ValueTask<MarketDataHistoricalManifest> AcquireAsync(
            MarketDataHistoricalRequest request,
            HistoricalAcquisitionCheckpoint checkpoint,
            IHistoricalObservationSink sink,
            CancellationToken token)
        {
            Requests.Add(request);
            long ordinal = 1;
            foreach (var series in request.Series)
            for (var date = request.StartDate; date <= request.EndDate; date = date.AddDays(1))
            {
                if (!calendar.IsTradingDate(date))
                    continue;
                var bounds = calendar.GetSession(date);
                var observation = new FuturesTradeSessionBarReadModel
                {
                    MarketSeriesIdentity = series.SeriesIdentity,
                    ObservationId = FuturesTradeSessionBarId.Create(
                        series.SeriesIdentity,
                        TimeFrameType.OneMinute,
                        bounds.StartUtc.AddMinutes(1),
                        ordinal),
                    ContractId = "ESZ24",
                    ValueDate = date,
                    TimeFrame = TimeFrameType.OneMinute,
                    IntervalStartUtc = bounds.StartUtc,
                    IntervalEndUtc = bounds.StartUtc.AddMinutes(1),
                    Open = 5000,
                    High = 5010,
                    Low = 4990,
                    Close = 5005,
                    Volume = 100,
                    TradeCount = 10,
                    PriceVolumeSum = 500_500,
                    FirstSourceSequence = ordinal,
                    LastSourceSequence = ordinal,
                    FirstMarketEventUtc = bounds.StartUtc,
                    LastMarketEventUtc = bounds.StartUtc.AddMinutes(1).AddTicks(-1),
                    CalculatedAtUtc = bounds.EndUtc,
                    CalculationVersion = "historical-daily-v1",
                    IsComplete = true,
                    IsValid = true,
                    CalculationMethod = MarketSignalCalculationMethod.NormalizedHistoricalAggregate
                };
                await sink.AcceptAsync(new NormalizedHistoricalBatch(
                    request.DataLoadAttemptId,
                    "fixture",
                    ordinal,
                    ordinal.ToString(),
                    [observation],
                    [],
                    $"{ordinal:X64}",
                    true), token);
                ordinal++;
            }
            return new MarketDataHistoricalManifest
            {
                ManifestId = Guid.NewGuid(),
                DataLoadAttemptId = request.DataLoadAttemptId,
                ProviderJobId = "fixture",
                RequestSha256 = $"{request.Series[0].SeriesIdentity.Format()}|{request.StartDate:O}|{request.EndDate:O}",
                NormalizedSha256 = "fixture",
                ObservationCount = ordinal - 1,
                FirstValueDate = request.StartDate,
                LastValueDate = request.EndDate,
                CompletedAtUtc = DateTimeOffset.UtcNow
            };
        }
    }

    sealed class MemoryDataLoaderStore : IHistoricalDataLoaderStore
    {
        public ValueTask<HistoricalDataLoaderState?> GetAsync(Guid id, CancellationToken token) => ValueTask.FromResult<HistoricalDataLoaderState?>(null);
        public ValueTask<HistoricalDataLoaderState?> GetCompletedByRequestHashAsync(string hash, CancellationToken token) => ValueTask.FromResult<HistoricalDataLoaderState?>(null);
        public ValueTask SaveAsync(HistoricalDataLoaderState state, CancellationToken token) => ValueTask.CompletedTask;
    }
}
