using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Feed.Command;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.MarketDataFeed;
public sealed class MarketOpenSessionTests
{
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

