using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Analytics.OptionVolatility;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.OptionVolatility;
public sealed class LiveOptionChainIvCollectorTests
{
    static EvaluatedOptionChainReadModel Chain(DateTimeOffset at, double call = .20, double put = .30) =>
        new("ES20261218", new(2026, 12, 18), 6000m, null, null, "Live", at,
        [Leg(true, at, call), Leg(false, at, put)]);
    static EvaluatedOptionContractReadModel Leg(bool call, DateTimeOffset at, double iv) =>
        new(call ? "C6000" : "P6000", 6000m, call, 10m, 11m, 1, 1, null, null, iv,
            10.5, .5, null, null, null, null, null, null, true, false, at, null, at);
    static LiveOptionChainIvCollector Collector(InMemoryOptionVolatilityRepository repository)
    {
        var environment = Substitute.For<IHostEnvironment>(); environment.EnvironmentName.Returns("Development");
        return new(repository, environment, NullLogger<LiveOptionChainIvCollector>.Instance);
    }
    [Fact] public async Task PublishesRealAtmIvAndExplainsMissingHistory()
    {
        var repository = new InMemoryOptionVolatilityRepository(); using var collector = Collector(repository);
        var chain = Chain(DateTimeOffset.UtcNow.AddSeconds(-1));
        await collector.PublishAsync(chain, new(2026, 10, 12));
        var result = await collector.GetLatestAsync(chain, default);
        result.Metric!.Snapshot.CurrentImpliedVolatility.Should().Be(.25m);
        result.Metric.Snapshot.RankStatus.Should().Be(VolatilityMetricStatus.InsufficientHistory);
        result.Metric.Snapshot.IvRank.Should().BeNull();
        result.Metric.Snapshot.SourceObservationIds.Should().ContainSingle();
    }
    [Theory] [InlineData("Frozen emulator preview")] [InlineData("stale")] [InlineData("missing put")]
    public async Task InvalidOrSyntheticInputsNeverPublish(string condition)
    {
        var repository = new InMemoryOptionVolatilityRepository(); using var collector = Collector(repository);
        var chain = Chain(DateTimeOffset.UtcNow);
        chain = condition switch { "Frozen emulator preview" => chain with { WindowMethod = condition },
            "stale" => chain with { Contracts = chain.Contracts.Select(x => x with { IsStale = true }).ToArray() },
            _ => chain with { Contracts = [chain.Contracts[0]] } };
        await collector.PublishAsync(chain, new(2026, 10, 12));
        (await collector.GetLatestAsync(chain, default)).Metric.Should().BeNull();
    }
    [Fact] public async Task CoalescesThenRevisesOneDailyObservationAcrossRestart()
    {
        var repository = new InMemoryOptionVolatilityRepository(); var date = new DateOnly(2026, 10, 12);
        var chain = Chain(DateTimeOffset.UtcNow.AddMinutes(-6));
        using (var first = Collector(repository)) { await first.PublishAsync(chain, date); await first.PublishAsync(chain, date); }
        using var restarted = Collector(repository);
        chain = Chain(DateTimeOffset.UtcNow, .30, .40); await restarted.PublishAsync(chain, date);
        var result = (await restarted.GetLatestAsync(chain, default)).Metric!.Snapshot;
        result.CurrentImpliedVolatility.Should().Be(.35m);
        result.ValidHistoricalObservationCount.Should().Be(0);
    }
    [Fact] public async Task CalculatesRankFromDistinctPriorDaysNotIntradayTicks()
    {
        var repository = new InMemoryOptionVolatilityRepository(); using var collector = Collector(repository);
        var today = new DateOnly(2026, 10, 12); var dates = new List<DateOnly>();
        for(var date = today.AddDays(-1); dates.Count < 20; date = date.AddDays(-1))
            if (date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) dates.Add(date);
        foreach(var date in dates.Order())
            await collector.PublishAsync(Chain(DateTimeOffset.UtcNow.AddDays(-today.DayNumber + date.DayNumber), .20, .20), date);
        var chain = Chain(DateTimeOffset.UtcNow, .30, .30); await collector.PublishAsync(chain, today);
        var result = (await collector.GetLatestAsync(chain, default)).Metric!.Snapshot;
        result.ValidHistoricalObservationCount.Should().Be(20);
        result.RankStatus.Should().Be(VolatilityMetricStatus.Qualified);
        result.IvRank.Should().Be(100m);
    }
}
