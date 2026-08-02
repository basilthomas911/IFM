using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Domain.Trade.Query.Api;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Query.Api;

public class ActorTradeQueryApiTests
{
    [Fact]
    public async Task TradeQuantityUsesDirectStorageAndReturnsTypedSuccess()
    {
        var (api, db) = CreateApi();
        db.GetTradeQuantityAsync(7).Returns(4);

        var result = await api.GetTradeQuantityAsync(7);

        api.Should().BeAssignableTo<IActorTradeQueryApi>();
        result.Success.Should().BeTrue();
        result.Value!.Value.Should().Be(4);
        await db.Received(1).GetTradeQuantityAsync(7);
    }

    [Fact]
    public async Task StorageFailureReturnsTheQueryErrorId()
    {
        var (api, db) = CreateApi();
        var exception = new InvalidOperationException("trade unavailable");
        db.GetTradeQuantityAsync(Arg.Any<int>())
            .Returns(_ => Task.FromException<int>(exception));

        var result = await api.GetTradeQuantityAsync(7);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(GetTradeQuantityQuery.ErrorId);
        result.ErrorMessage.Should().Be(exception.Message);
    }

    [Fact]
    public async Task TradePlanSummaryIsExplicitlyNotImplementedPendingUiRemoval()
    {
        var (api, _) = CreateApi();

        var action = () => api.GetTradePlanSummaryAsync(1, 2, new DateOnly(2026, 1, 5));

        await action.Should().ThrowAsync<NotImplementedException>();
    }

    static (ActorTradeQueryApi Api, ITradeDbContext Db) CreateApi()
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        var db = Substitute.For<ITradeDbContext>();
        dbFactory.TradeDb.Returns(db);
        return (new ActorTradeQueryApi(dbFactory, Substitute.For<IBlackboardService>()), db);
    }
}
