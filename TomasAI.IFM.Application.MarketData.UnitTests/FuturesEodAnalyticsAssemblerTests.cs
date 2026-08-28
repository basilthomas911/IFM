using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

/// <summary>Verifies the raw-EOD compatibility boundary never recalculates analytics values.</summary>
public sealed class FuturesEodAnalyticsAssemblerTests
{
    /// <summary>Uses only independently stored signals with the exact raw observation identity.</summary>
    [Fact]
    public async Task AssembleAsync_UsesExactPersistedSignalsAndReportsMissingFamilies()
    {
        var series = MarketSeriesIdentity.ForContract("ESZ26");
        var valueDate = new DateOnly(2026, 8, 25);
        var observationId = FuturesTradeSessionBarId.Create(
            series, TimeFrameType.Daily, new DateTimeOffset(2026, 8, 25, 21, 0, 0, TimeSpan.Zero), 42);
        var raw = Raw(series, valueDate, observationId);
        var signal = new MarketAnalyticsSignalResult(
            MarketAnalyticsSignalFamily.Ema,
            Metadata(series, valueDate, observationId),
            new { Ema10 = 6401m });
        var assembler = new FuturesEodAnalyticsAssembler(
            new ObservationStore(raw), new SignalReader(signal));

        var result = await assembler.AssembleAsync(
            series, valueDate,
            [MarketAnalyticsSignalFamily.Ema, MarketAnalyticsSignalFamily.BollingerBand],
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Same(raw, result.Raw);
        Assert.Same(signal, result.Signals[MarketAnalyticsSignalFamily.Ema]);
        Assert.Equal([MarketAnalyticsSignalFamily.BollingerBand], result.MissingSignals);
    }

    /// <summary>Rejects a signal reader that returns data derived from a different observation.</summary>
    [Fact]
    public async Task AssembleAsync_RejectsMismatchedObservationIdentity()
    {
        var series = MarketSeriesIdentity.ForContract("ESZ26");
        var valueDate = new DateOnly(2026, 8, 25);
        var rawId = FuturesTradeSessionBarId.Create(
            series, TimeFrameType.Daily, new DateTimeOffset(2026, 8, 25, 21, 0, 0, TimeSpan.Zero), 42);
        var otherId = FuturesTradeSessionBarId.Create(
            series, TimeFrameType.Daily, new DateTimeOffset(2026, 8, 25, 21, 0, 0, TimeSpan.Zero), 43);
        var assembler = new FuturesEodAnalyticsAssembler(
            new ObservationStore(Raw(series, valueDate, rawId)),
            new SignalReader(new(
                MarketAnalyticsSignalFamily.Ema,
                Metadata(series, valueDate, otherId),
                new { Ema10 = 6401m })));

        await Assert.ThrowsAsync<InvalidDataException>(() => assembler.AssembleAsync(
            series, valueDate, [MarketAnalyticsSignalFamily.Ema], CancellationToken.None).AsTask());
    }

    static FuturesEodObservationReadModel Raw(
        MarketSeriesIdentity series,
        DateOnly valueDate,
        FuturesTradeSessionBarId observationId) => new()
    {
        MarketSeriesIdentity = series, ContractId = "ESZ26", ValueDate = valueDate,
        SessionStartUtc = new DateTimeOffset(2026, 8, 24, 22, 0, 0, TimeSpan.Zero),
        SessionEndUtc = new DateTimeOffset(2026, 8, 25, 21, 0, 0, TimeSpan.Zero),
        Open = 6400m, High = 6425m, Low = 6375m, Close = 6410m,
        Volume = 1000m, TradeCount = 10, PriceVolumeSum = 6_410_000m,
        ObservationId = observationId, FirstSourceSequence = 1, LastSourceSequence = 42,
        FirstMarketEventUtc = new DateTimeOffset(2026, 8, 24, 22, 0, 1, TimeSpan.Zero),
        LastMarketEventUtc = new DateTimeOffset(2026, 8, 25, 20, 59, 59, TimeSpan.Zero),
        IsComplete = true, IsValid = true
    };

    static MarketAnalyticsSignalMetadata Metadata(
        MarketSeriesIdentity series,
        DateOnly valueDate,
        FuturesTradeSessionBarId observationId) => new()
    {
        SignalKey = new(series, MarketAnalyticsSignalKind.Ema, TimeFrameType.Daily, "ema-v1"),
        ContractId = "ESZ26", ValueDate = valueDate, ObservationId = observationId,
        MarketDataAsOfUtc = new DateTimeOffset(2026, 8, 25, 20, 59, 59, TimeSpan.Zero),
        CalculatedAtUtc = new DateTimeOffset(2026, 8, 25, 21, 0, 0, TimeSpan.Zero),
        SourceSequence = 42, SchemaVersion = 1, CalculationVersion = "ema-v1",
        CalculationMethod = MarketSignalCalculationMethod.ClosedObservation,
        IsValid = true, ValidationIssues = []
    };

    sealed class ObservationStore(FuturesEodObservationReadModel raw) : IHistoricalObservationStore
    {
        public ValueTask<bool> TryWriteObservationAsync(FuturesTradeSessionBarReadModel observation, CancellationToken cancellationToken) => ValueTask.FromResult(true);
        public ValueTask<bool> TryWriteRawEodAsync(FuturesEodObservationReadModel observation, CancellationToken cancellationToken) => ValueTask.FromResult(true);
        public ValueTask<FuturesEodObservationReadModel?> GetRawEodAsync(MarketSeriesIdentity seriesIdentity, DateOnly valueDate, CancellationToken cancellationToken)
            => ValueTask.FromResult<FuturesEodObservationReadModel?>(raw);
    }

    sealed class SignalReader(MarketAnalyticsSignalResult? signal) : IHistoricalAnalyticsSignalReader
    {
        public ValueTask<MarketAnalyticsSignalResult?> GetExactAsync(
            MarketAnalyticsSignalFamily family,
            MarketSeriesIdentity seriesIdentity,
            TimeFrameType timeFrame,
            FuturesTradeSessionBarId observationId,
            CancellationToken cancellationToken)
            => ValueTask.FromResult(signal?.Family == family ? signal : null);
    }
}
