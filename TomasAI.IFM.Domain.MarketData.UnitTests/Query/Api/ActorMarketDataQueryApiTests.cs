using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Domain.MarketData.Query.Api;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.UnitTests.Query.Api;

public class ActorMarketDataQueryApiTests
{
    [Fact]
    public async Task TradingDaysUseDirectStorageAndReturnTypedSuccess()
    {
        var (api, db) = CreateApi();
        var startDate = new DateOnly(2026, 1, 5);
        var endDate = startDate.AddDays(2);
        db.GetTradingDatesAsync(startDate, endDate, MarketType.Futures, CurrencyType.USD)
            .Returns([startDate, endDate]);

        var result = await api.GetTradingDaysAsync(
            startDate, endDate, MarketType.Futures, CurrencyType.USD);

        api.Should().BeAssignableTo<IActorMarketDataQueryApi>();
        result.Success.Should().BeTrue();
        result.Value!.Value.Should().Be(2);
    }

    [Fact]
    public async Task StorageFailureReturnsTheQueryErrorId()
    {
        var (api, db) = CreateApi();
        var exception = new InvalidOperationException("market data unavailable");
        db.GetTradingDatesAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(),
                Arg.Any<MarketType>(), Arg.Any<CurrencyType>())
            .Returns(_ => Task.FromException<DateOnly[]>(exception));

        var result = await api.GetTradingDaysAsync(
            new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 6),
            MarketType.Futures, CurrencyType.USD);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(GetTradingDaysQuery.ErrorId);
        result.ErrorMessage.Should().Be(exception.Message);
    }

    static (ActorMarketDataQueryApi Api, IMarketDataDbContext Db) CreateApi()
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        var db = Substitute.For<IMarketDataDbContext>();
        dbFactory.MarketDataDb.Returns(db);
        dbFactory.SecuritiesDb.Returns(Substitute.For<ISecuritiesDbContext>());
        return (new ActorMarketDataQueryApi(dbFactory), db);
    }
}
