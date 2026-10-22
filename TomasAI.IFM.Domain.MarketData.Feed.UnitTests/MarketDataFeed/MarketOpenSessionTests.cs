using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Command;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.MarketDataFeed;
public sealed class MarketOpenSessionTests
{
    [Fact]
    public async Task Feed_start_resolves_prepared_contracts_only_through_the_market_data_API()
    {
        var context = Substitute.For<TomasAI.IFM.Domain.MarketData.Feed.Command.Actor.IMarketDataFeedCommandContext>();
        var market = Substitute.For<TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi>();
        context.Container.Resolve<TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi>().Returns(market);
        var command = new StartMarketDataFeedCommand { CommandId = Guid.NewGuid(), EntityId = SampleData.FeedEntityId };
        market.PrepareSessionAsync(command.ValueDate, Arg.Any<CancellationToken>()).Returns([SampleData.EsContract]);
        var state = new TomasAI.IFM.Domain.MarketData.Feed.Command.State.MarketDataFeedCommandState();
        var result = await TomasAI.IFM.Domain.MarketData.Feed.Command.Actor.MarketDataFeedCommandActor.ExecuteStartAsync(command, context, state);
        Assert.IsType<TomasAI.IFM.Shared.EventSourcing.ServiceOk<TomasAI.IFM.Shared.EventSourcing.GuidResult>>(result);
        await market.Received(1).PrepareSessionAsync(command.ValueDate, Arg.Any<CancellationToken>());
        Assert.Single(state.Events);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Backend_resolves_date_without_market_hours_or_pending_eod_admission(bool open, bool pending)
    {
        var date = new DateOnly(2026,10,16);
        var session = new MarketSessionReadModel {
            OperationalValueDate=date, ActiveValueDate=open ? date : null,
            State=open ? FuturesMarketState.OffTrading : FuturesMarketState.Closed,
            IsEndOfDayPending=pending,
            SessionStartUtc=new(2026,10,15,22,0,0,DateTimeKind.Utc),
            SessionEndUtc=new(2026,10,16,21,0,0,DateTimeKind.Utc),
            NextTransitionUtc=new(2026,10,15,21,0,0,DateTimeKind.Utc),
            Revision=1, AsOfUtc=new(2026,10,15,12,0,0,DateTimeKind.Utc)
        };
        new StartMarketDataFeedCommand().ResolveSession(session, new DateTimeOffset(2026,10,15,12,0,0,TimeSpan.Zero))
            .ValueDate.Should().Be(new DateOnly(2026,10,15));
    }
}

