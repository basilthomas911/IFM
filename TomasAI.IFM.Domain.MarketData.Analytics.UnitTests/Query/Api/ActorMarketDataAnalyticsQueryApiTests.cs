using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.Query.Api;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.Query.Api;

public class ActorMarketDataAnalyticsQueryApiTests
{
    [Fact]
    public async Task TradeSignalIdsUseDirectStorageAndReturnTypedSuccess()
    {
        var (api, db) = CreateApi();
        var valueDate = new DateOnly(2026, 1, 5);
        db.GetFuturesTradeSignalIdByValueDateAsync(valueDate)
            .Returns(Array.Empty<FuturesTradeSignalId>());

        var result = await api.GetFuturesTradeSignalIdsAsync(valueDate);

        api.Should().BeAssignableTo<IActorMarketDataAnalyticsQueryApi>();
        result.Success.Should().BeTrue();
        result.Value.Should().BeEmpty();
        await db.Received(1).GetFuturesTradeSignalIdByValueDateAsync(valueDate);
    }

    [Fact]
    public async Task StorageFailureReturnsTheQueryErrorId()
    {
        var (api, db) = CreateApi();
        var exception = new InvalidOperationException("analytics unavailable");
        db.GetFuturesTradeSignalIdByValueDateAsync(Arg.Any<DateOnly>())
            .Returns(_ => Task.FromException<ICollection<FuturesTradeSignalId>>(exception));

        var result = await api.GetFuturesTradeSignalIdsAsync(new DateOnly(2026, 1, 5));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(GetFuturesTradeSignalIdsQuery.ErrorId);
        result.ErrorMessage.Should().Be(exception.Message);
    }

    static (ActorMarketDataAnalyticsQueryApi Api, IMarketDataDbContext Db) CreateApi()
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        var db = Substitute.For<IMarketDataDbContext>();
        dbFactory.MarketDataDb.Returns(db);
        return (new ActorMarketDataAnalyticsQueryApi(dbFactory), db);
    }
}
