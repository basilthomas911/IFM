using FluentAssertions;
using TomasAI.IFM.Application.Storage.TradePlanDb;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.TradePlanDb;

public sealed class TradePlanChartCacheTests
{
    [Fact]
    public async Task Concurrent_chart_requests_reuse_samples_but_revision_scopes_are_independent()
    {
        var plan = TradePlanStorageTests.Plan(Random.Shared.Next(6_000_000, 9_000_000), 1, -42m, true);
        var key = (Guid.NewGuid().ToString(), plan.Position.Id, plan.Position.StrategyKind, plan.ValueDate, (long?)null);
        var calls = 0;
        Task<TradePlanChartCache.Series> Load()
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(new TradePlanChartCache.Series(plan.CalculatedAtUtc,
                [new StrategyTradePlanChartPoint(plan.CalculatedAtUtc, -42m)]));
        }
        var results = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => TradePlanChartCache.ReadAsync(key, Load, CancellationToken.None)));
        calls.Should().Be(1);
        results.Should().OnlyContain(series => series.Points.Single().NetSpread == -42m);
        key.Item5 = 1;
        await TradePlanChartCache.ReadAsync(key, Load, CancellationToken.None);
        calls.Should().Be(2);
    }

    [Fact]
    public async Task Failed_chart_reads_are_not_cached_and_do_not_block_subsequent_reads()
    {
        var plan = TradePlanStorageTests.Plan(Random.Shared.Next(6_000_000, 9_000_000), 1, -42m, true);
        var key = (Guid.NewGuid().ToString(), plan.Position.Id, plan.Position.StrategyKind, plan.ValueDate, (long?)null);
        var failed = () => TradePlanChartCache.ReadAsync(key,
            () => throw new InvalidOperationException("Unavailable"), CancellationToken.None);
        await failed.Should().ThrowAsync<InvalidOperationException>();
        var result = await TradePlanChartCache.ReadAsync(key,
            () => Task.FromResult(new TradePlanChartCache.Series(plan.CalculatedAtUtc, [])), CancellationToken.None);
        result.Points.Should().BeEmpty();
    }
}
