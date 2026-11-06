using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.FeedSessionPreparation.Function;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ProducerOperations;
using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.MarketDataFeed;

public sealed class MarketOpenSessionTests
{
    [Fact]
    public async Task Opening_reads_API_provider_without_advancing_the_date()
    {
        var dates = Substitute.For<IValueDateProvider, IMarketOpenValueDateInitializer>();
        var date = new DateOnly(2026, 10, 30);
        dates.ValueDate.Returns(date);
        var market = Substitute.For<IMarketDataApi>();
        market.PrepareSessionAsync(date, CancellationToken.None).Returns([SampleData.EsContract]);
        var request = PrepareMarketDataFeedSessionCommand.ForOperation(Guid.NewGuid());

        var prepared = await request.ExecuteAsync(dates, market, CancellationToken.None);

        prepared.ValueDate.Should().Be(date);
        prepared.Contracts.Should().Equal(SampleData.EsContract);
        prepared.CommandId.Should().Be(request.CommandId);
        ((IMarketOpenValueDateInitializer)dates).DidNotReceive().InitializeMarketOpen();
        await market.Received(1).PrepareSessionAsync(date, CancellationToken.None);
    }

    [Theory]
    [InlineData(2001, 1, 3)]
    [InlineData(2026, 10, 29)]
    [InlineData(2035, 6, 12)]
    public async Task Preparation_uses_provider_date_including_backtesting_dates(int year, int month, int day)
    {
        var date = new DateOnly(year, month, day);
        var dates = Substitute.For<IValueDateProvider>();
        dates.ValueDate.Returns(date);
        var market = Substitute.For<IMarketDataApi>();
        market.PrepareSessionAsync(date, CancellationToken.None).Returns([SampleData.EsContract]);
        var request = PrepareMarketDataFeedSessionCommand.ForOperation(Guid.NewGuid());

        (await request.ExecuteAsync(dates, market, CancellationToken.None)).ValueDate.Should().Be(date);
        _ = dates.Received(1).ValueDate;
    }

    [Fact]
    public async Task Uninitialized_value_date_is_rejected_without_calling_the_backend()
    {
        var dates = Substitute.For<IValueDateProvider>();
        var market = Substitute.For<IMarketDataApi>();
        var request = PrepareMarketDataFeedSessionCommand.ForOperation(Guid.NewGuid());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            request.ExecuteAsync(dates, market, CancellationToken.None).AsTask());
        await market.DidNotReceiveWithAnyArgs().PrepareSessionAsync(default);
    }
}
