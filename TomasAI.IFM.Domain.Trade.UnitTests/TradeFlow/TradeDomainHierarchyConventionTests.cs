using FluentAssertions;
using TomasAI.IFM.Domain.Trade.Futures.Option.Event.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Queries;

namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;

public sealed class TradeDomainHierarchyConventionTests
{
    [Fact]
    public void Production_trade_types_do_not_use_retired_trade_or_option_roots()
    {
        var invalidTypes = typeof(FuturesOptionTradeQueryActor).Assembly
            .GetTypes()
            .Where(type =>
                type.Namespace?.StartsWith(
                    "TomasAI.IFM.Domain.Trade.Trade",
                    StringComparison.Ordinal) == true
                || type.Namespace?.StartsWith(
                    "TomasAI.IFM.Domain.Trade.Option",
                    StringComparison.Ordinal) == true)
            .Select(type => type.FullName)
            .Order()
            .ToArray();

        invalidTypes.Should().BeEmpty();
    }

    [Fact]
    public void Legacy_option_query_contracts_route_to_the_futures_option_query_actor()
    {
        string[] routes =
        [
            GetOptionTradeQuery.Actor,
            GetOptionTradesQuery.Actor,
            GetOptionTradeSpreadDataQuery.Actor,
            GetOptionTradeSpreadBarDataQuery.Actor,
            GetOptionLegContractIdsQuery.Actor,
            GetIronCondorTradePriceQuery.Actor,
            GetTradePositionsQuery.Actor,
            GetTradePositionTradeTypesQuery.Actor,
            GetTradePlanActionQuery.Actor,
            GetIronCondorMDILimitQuery.Actor
        ];

        routes.Should().OnlyContain(route =>
            route == FuturesOptionTradeQueryActor.ActorName);
    }

    [Fact]
    public void Compatibility_option_events_route_to_the_futures_option_event_actor()
    {
        OptionTradeEndOfDayProcessedEvent.Actor
            .Should().Be(FuturesOptionTradeEventActor.ActorName);
        OptionTradeLegDataChangedEvent.Actor
            .Should().Be(FuturesOptionTradeEventActor.ActorName);
    }
}
