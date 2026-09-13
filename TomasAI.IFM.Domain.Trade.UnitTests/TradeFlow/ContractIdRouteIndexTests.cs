using FluentAssertions;
using TomasAI.IFM.Domain.Trade.Futures.Realtime.Model;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;

public sealed class ContractIdRouteIndexTests
{
    [Fact]
    public void One_contract_routes_to_every_open_position_without_allocating_a_lookup_collection()
    {
        var index = new ContractIdRouteIndex();
        var first = Route(Guid.NewGuid(), Guid.NewGuid(), 1, 10);
        var second = Route(Guid.NewGuid(), Guid.NewGuid(), 1, 11);
        index.Add(first, "ESZ6").Should().BeTrue();
        index.Add(second, "ESZ6").Should().BeTrue();
        var tick = new PositionMarketTick("ESZ6", 5200.25m, 10,
            new DateTime(2026, 9, 14, 14, 0, 0, DateTimeKind.Utc));

        index.TryRoute(in tick, out var routes).Should().Be(MarketRouteLookupOutcome.Routed);
        routes.Should().Equal(first, second);
        index.TryGetRoutes("ESZ6", out var sameRoutes).Should().BeTrue();
        ReferenceEquals(routes, sameRoutes).Should().BeTrue();
    }

    [Fact]
    public void New_generation_replaces_all_position_legs_and_stale_lifecycle_changes_are_ignored()
    {
        var index = new ContractIdRouteIndex();
        var positionId = Guid.NewGuid();
        var oldLeg = Route(positionId, Guid.NewGuid(), 1, 10);
        var newLeg = Route(positionId, Guid.NewGuid(), 2, 10);
        index.Add(oldLeg, "ESZ6-old").Should().BeTrue();

        index.Add(newLeg, "ESZ6-new").Should().BeTrue();
        index.TryGetRoutes("ESZ6-old", out _).Should().BeFalse();
        index.RemovePosition(positionId, 1).Should().Be(0);
        index.TryGetRoutes("ESZ6-new", out var routes).Should().BeTrue();
        routes.Should().ContainSingle().Which.Should().Be(newLeg);

        index.RemovePosition(positionId, 3).Should().Be(1);
        index.Add(newLeg, "ESZ6-new").Should().BeFalse();
    }

    [Fact]
    public void Invalid_duplicate_and_unrouted_ticks_are_classified_without_throwing()
    {
        var index = new ContractIdRouteIndex();
        index.RegisterKnownContract("ESZ6");
        var at = new DateTime(2026, 9, 14, 14, 0, 0, DateTimeKind.Utc);
        var unrouted = new PositionMarketTick("ESZ6", 5200, 1, at);
        index.TryRoute(in unrouted, out _).Should().Be(MarketRouteLookupOutcome.NoOpenPosition);
        index.TryRoute(in unrouted, out _).Should().Be(MarketRouteLookupOutcome.DuplicateOrOutOfOrder);
        var invalid = new PositionMarketTick(string.Empty, 0, 0, DateTime.MinValue);
        index.TryRoute(in invalid, out _).Should().Be(MarketRouteLookupOutcome.InvalidTick);
    }

    static PortfolioFundTradeLeg Route(Guid positionId, Guid legId, long generation, int fundId) =>
        new(1, fundId, 20 + fundId, 30 + fundId, positionId, legId,
            TradeStrategyKind.FuturesOutright, generation);
}
