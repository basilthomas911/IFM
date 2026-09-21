using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using TomasAI.IFM.Domain.MarketData.Analytics.OptionVolatility;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Benchmarks;

/// <summary>Measures the pure Stage 4 IV Rank/Percentile calculation over configured prior-session windows.</summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 2, iterationCount: 5)]
public class Stage4VolatilityCalculationBenchmarks
{
    VolatilityMetricPolicy policy = null!;
    VolatilityCalculationWindow window = null!;
    VolatilityCalculationObservation current = null!;
    VolatilityCalculationObservation[] history = null!;

    [Params(32, 252)]
    public int PriorSessionCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var currentDate = new DateOnly(2026, 9, 18);
        var priorDates = new DateOnly[PriorSessionCount];
        history = new VolatilityCalculationObservation[PriorSessionCount];

        for (var index = 0; index < PriorSessionCount; index++)
        {
            var date = currentDate.AddDays(index - PriorSessionCount);
            priorDates[index] = date;
            history[index] = new(
                $"stage4-history-{index:D3}",
                date,
                0.15m + index % 41 * 0.0025m,
                VolatilityValueUnit.AnnualDecimal,
                VolatilityObservationStatus.Qualified);
        }

        policy = new(
            "stage4-benchmark-policy-v1",
            PriorSessionCount,
            PriorSessionCount,
            1m,
            VolatilityGapPolicy.PreserveExpectedSessionGap,
            VolatilityHistoricalWindowConvention.PriorExchangeSessions,
            VolatilityRankRangeConvention.CurrentAndPriorWindow,
            VolatilityPercentileTieConvention.StrictlyLessThanCurrent);
        window = new(currentDate, priorDates.ToImmutableArray());
        current = new(
            "stage4-current",
            currentDate,
            0.225m,
            VolatilityValueUnit.AnnualDecimal,
            VolatilityObservationStatus.Qualified);
    }

    [Benchmark]
    public VolatilityMetricCalculation Calculate() =>
        VolatilityRankPercentileCalculator.Calculate(policy, window, current, history);
}

/// <summary>Measures bounded in-memory metric history paging through the production asynchronous API.</summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 2, iterationCount: 5)]
public class Stage4VolatilityMetricHistoryBenchmarks
{
    const int StoredMetricCount = 256;
    static readonly DateOnly ValueDate = new(2026, 9, 18);
    static readonly DateTimeOffset AvailableAtUtc = new(2026, 9, 18, 21, 0, 0, TimeSpan.Zero);
    static readonly VolatilitySeriesIdentity Series = new("stage4-benchmark-series", "method-v1");
    static readonly VolatilityStorageScope Scope = new("benchmark", Series, "metric-policy-v1");

    InMemoryOptionVolatilityRepository repository = null!;
    VolatilityHistoryPageRequest request = null!;

    [Params(32, 256)]
    public int PageSize { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        repository = new();
        var publications = new OptionIvPublication[StoredMetricCount];
        for (var index = 0; index < publications.Length; index++)
            publications[index] = CreatePublication(index);

        foreach (var publication in publications)
            await repository.PublishAsync(publication).ConfigureAwait(false);

        request = new(
            Scope,
            VolatilityCalendarBucket.From(ValueDate),
            ValueDate,
            ValueDate,
            null,
            VolatilityHistoricalMode.Restated,
            null,
            PageSize,
            null);
    }

    [Benchmark]
    public Task<VolatilityPage<OptionIvMetricRevision>> GetMetricHistoryPageAsync() =>
        repository.GetMetricHistoryAsync(request);

    static OptionIvPublication CreatePublication(int index)
    {
        var suffix = index.ToString("D3");
        var observationId = $"stage4-observation-{suffix}";
        var snapshotId = $"stage4-snapshot-{suffix}";
        var samplingSlot = $"slot-{suffix}";
        var impliedVolatility = 0.15m + index % 41 * 0.0025m;
        var observation = new OptionIvObservation(
            OptionIvObservation.CurrentSchemaVersion,
            observationId,
            Series,
            ValueDate,
            samplingSlot,
            impliedVolatility,
            VolatilityValueUnit.AnnualDecimal,
            VolatilityObservationStatus.Qualified,
            string.Empty,
            AvailableAtUtc.AddMinutes(-2),
            AvailableAtUtc.AddMinutes(-1),
            AvailableAtUtc,
            1,
            null,
            new([], "benchmark-pricer-v1", $"input-{suffix}", "stage4-benchmark-evidence"));
        var snapshot = new OptionIvMetricSnapshot(
            OptionIvMetricSnapshot.CurrentSchemaVersion,
            snapshotId,
            $"snapshot-digest-{suffix}",
            Series,
            Scope.MetricPolicyVersion,
            ValueDate,
            samplingSlot,
            impliedVolatility,
            VolatilityValueUnit.AnnualDecimal,
            50m,
            VolatilityMetricStatus.Qualified,
            50m,
            VolatilityMetricStatus.Qualified,
            VolatilityMetricUnit.PercentagePoints0To100,
            0.15m,
            0.25m,
            index / 2,
            0,
            252,
            252,
            1m,
            ValueDate.AddDays(-252),
            ValueDate.AddDays(-1),
            [observationId],
            $"sources-{suffix}",
            "stage4-benchmark-calculator-v1",
            AvailableAtUtc.AddMinutes(-2),
            AvailableAtUtc.AddMinutes(-1),
            AvailableAtUtc.AddSeconds(-1),
            AvailableAtUtc);

        return new(
            Scope.Environment,
            new(snapshot, 1, null, index + 1L),
            [observation]);
    }
}
