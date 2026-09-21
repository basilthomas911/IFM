using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.OptionVolatility;

internal static class OptionVolatilityTestData
{
    internal static readonly DateTimeOffset Now = new(2026, 9, 18, 20, 0, 0, TimeSpan.Zero);
    internal static readonly DateOnly ValueDate = new(2026, 9, 18);
    internal static readonly VolatilitySeriesIdentity Series = new("ES-ATM-30D", "method-v1");
    internal static readonly VolatilityStorageScope Scope = new("simulation", Series, "metric-v1");

    internal static VolatilitySeriesDefinition Definition(
        VolatilityOptionSideSelection side = VolatilityOptionSideSelection.CallPutCombined,
        VolatilityInterpolationMethod interpolation = VolatilityInterpolationMethod.LinearTotalVariance) =>
        new(
            VolatilitySeriesDefinition.CurrentSchemaVersion,
            Series,
            "ES",
            "XCME",
            "USD",
            new("simulation", "fixture", "quotes-v1"),
            new(30, VolatilityMoneynessConvention.AtTheMoneyForward, side, ["EW"],
                side == VolatilityOptionSideSelection.CallPutCombined
                    ? VolatilitySideCombinationMethod.ArithmeticMean
                    : VolatilitySideCombinationMethod.None),
            new("European", "FuturesStyle", "DeliveryOfFuture", ["black76-v1"], "solver-v1"),
            new("mid", "quality-v1", true, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1),
                TimeSpan.FromSeconds(5)),
            new("bracket-v1", interpolation, "roll-v1", "calendar-v1", "America/Chicago",
                "daily-v1", TimeSpan.FromMinutes(5), 2),
            new("metric-v1", 2, 2, 1m, VolatilityGapPolicy.PreserveExpectedSessionGap,
                VolatilityHistoricalWindowConvention.PriorExchangeSessions,
                VolatilityRankRangeConvention.CurrentAndPriorWindow,
                VolatilityPercentileTieConvention.StrictlyLessThanCurrent),
            new(new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), null, "config-v1", "market-data",
                "evidence-v1", []));

    internal static QualifiedOptionIvInput Input(
        string id,
        int tenorDays,
        decimal strike,
        bool call,
        decimal iv,
        string underlying = "ESZ6",
        decimal underlyingPrice = 5000m) =>
        new(id, underlying, "ES", "simulation", "EW", "XCME", "USD", "fixture", "quotes-v1",
            "European", "FuturesStyle", "DeliveryOfFuture", "black76-v1", "solver-v1",
            Now.AddDays(tenorDays), strike, call, iv, underlyingPrice, Now.AddSeconds(-2),
            Now.AddSeconds(-3), id.GetHashCode(StringComparison.Ordinal), 100,
            Guid.Parse("11111111-1111-1111-1111-111111111111"), $"digest-{id}");

    internal static OptionIvObservation Observation(
        string id,
        DateOnly? date = null,
        decimal iv = 0.25m,
        int revision = 1,
        string? supersedes = null,
        DateTimeOffset? available = null,
        string slot = "daily-close",
        string underlying = "ESZ6") =>
        new(OptionIvObservation.CurrentSchemaVersion, id, Series, date ?? ValueDate, slot, iv,
            VolatilityValueUnit.AnnualDecimal, VolatilityObservationStatus.Qualified, string.Empty,
            (available ?? Now).AddMinutes(-1), (available ?? Now).AddSeconds(-1), available ?? Now,
            revision, supersedes,
            new([new($"option-{id}", underlying, Now.AddSeconds(-2), Now.AddSeconds(-3), 1, 2,
                    Guid.Parse("22222222-2222-2222-2222-222222222222"))],
                "black76-v1", $"digest-{id}", "evidence-v1"));

    internal static OptionIvMetricRevision Metric(
        string snapshotId,
        long sequence,
        OptionIvObservation source,
        int revision = 1,
        string? supersedes = null,
        DateTimeOffset? available = null) =>
        new(new(
                OptionIvMetricSnapshot.CurrentSchemaVersion, snapshotId, $"digest-{snapshotId}", Series,
                "metric-v1", source.ExchangeValueDate, source.SamplingSlot, source.ImpliedVolatility,
                VolatilityValueUnit.AnnualDecimal, 50m, VolatilityMetricStatus.Qualified, 50m,
                VolatilityMetricStatus.Qualified, VolatilityMetricUnit.PercentagePoints0To100,
                0.10m, 0.40m, 1, 0, 2, 2, 1m, source.ExchangeValueDate.AddDays(-2),
                source.ExchangeValueDate.AddDays(-1), [source.ObservationId], $"sources-{snapshotId}",
                "calculator-v1", source.ObservedAtUtc, (available ?? Now).AddSeconds(-2),
                (available ?? Now).AddSeconds(-1), available ?? Now),
            revision, supersedes, sequence);

    internal static OptionIvPublication Publication(
        string snapshotId,
        long sequence,
        OptionIvObservation? source = null,
        int revision = 1,
        string? supersedes = null,
        DateTimeOffset? available = null)
    {
        source ??= Observation($"observation-{snapshotId}", revision: revision,
            supersedes: revision == 1 ? null : $"observation-{supersedes}", available: available);
        return new("simulation", Metric(snapshotId, sequence, source, revision, supersedes, available), [source]);
    }

    internal static VolatilityHistoryPageRequest History(
        VolatilityHistoricalMode mode = VolatilityHistoricalMode.Restated,
        DateTimeOffset? knownAt = null,
        int pageSize = 50,
        byte[]? pagingState = null,
        DateOnly? from = null,
        DateOnly? to = null,
        string? slot = null) =>
        new(Scope, VolatilityCalendarBucket.From(from ?? ValueDate), from ?? ValueDate, to ?? ValueDate,
            slot, mode, knownAt, pageSize, pagingState);
}
