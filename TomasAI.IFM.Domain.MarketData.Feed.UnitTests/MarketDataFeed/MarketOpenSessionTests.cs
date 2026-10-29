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
    [InlineData(2001, 1, 3)]
    [InlineData(2026, 10, 29)]
    [InlineData(2035, 6, 12)]
    public void Feed_start_uses_provider_date_including_backtesting_dates(int year, int month, int day)
    {
        var date = new DateOnly(year, month, day);
        var dates = Substitute.For<IValueDateProvider>();
        dates.ValueDate.Returns(date);
        var command = new StartMarketDataFeedCommand { EntityId = new(new DateOnly(1999, 1, 4)) };

        var resolved = command.ResolveSession(dates);

        resolved.ValueDate.Should().Be(date);
        resolved.Subject.EntityId.Should().Be(new TomasAI.IFM.Domain.MarketData.Feed.Shared.MarketDataFeedId(date).Format());
        _ = dates.Received(1).ValueDate;
    }

    [Fact]
    public void Uninitialized_value_date_is_rejected_without_a_clock_fallback()
    {
        var dates = Substitute.For<IValueDateProvider>();
        Action start = () => new StartMarketDataFeedCommand().ResolveSession(dates);
        start.Should().Throw<InvalidOperationException>().WithMessage("*value-date provider*");
    }
}
