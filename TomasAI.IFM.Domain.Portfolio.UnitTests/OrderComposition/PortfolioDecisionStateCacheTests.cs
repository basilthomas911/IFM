using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.OrderComposition.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.OrderComposition;

public sealed class PortfolioDecisionStateCacheTests
{
    [Fact]
    public void Ready_reads_do_not_load_and_mutation_invalidates_the_published_generation()
    {
        var cache = new PortfolioDecisionStateCache();
        var now = DateTime.UtcNow;
        var portfolio = Random.Shared.Next(100000, int.MaxValue);
        cache.TryAcquire(portfolio, 7, now, out _).Should().BeFalse();
        var state = new PortfolioDecisionFinancialState(new FinancialBookConfiguration
        {
            PortfolioId = portfolio, Funds = [new() { FundId = 7, Reference = new() { ValidUntilUtc = now.AddMinutes(1) } }]
        }, 12, [new(7, 10000, [])], now, now.AddSeconds(3), FinancialDecisionStateInvalidation.Current(portfolio));
        cache.Publish(state);
        cache.TryAcquire(portfolio, 7, now, out var ready).Should().BeTrue();
        ready.Should().BeSameAs(state);
        cache.TryAcquire(portfolio, 8, now, out _).Should().BeFalse();
        cache.TryAcquire(portfolio, 7, state.ValidUntilUtc, out _).Should().BeFalse();
        FinancialDecisionStateInvalidation.Invalidate(portfolio);
        cache.TryAcquire(portfolio, 7, now, out _).Should().BeFalse();
        var refreshed = state with { Generation = FinancialDecisionStateInvalidation.Current(portfolio), CapturedAtUtc = now.AddMilliseconds(1) };
        cache.Publish(refreshed);
        cache.Publish(state);
        cache.TryAcquire(portfolio, 7, now.AddMilliseconds(1), out ready).Should().BeTrue();
        ready.Should().BeSameAs(refreshed);
        cache.Publish(state with { FinancialRevision = 11 });
        cache.TryAcquire(portfolio, 7, now.AddMilliseconds(1), out ready).Should().BeTrue();
        ready!.FinancialRevision.Should().Be(12);
    }
}
