using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Feed.Query.Api;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.Query.Api;

public class ActorMarketDataFeedQueryApiTests
{
    [Fact]
    public async Task NormalCurveUsesDirectStorageAndReturnsTypedSuccess()
    {
        var (api, db) = CreateApi();
        var model = new NormalCurveTableReadModel();
        db.GetNormalCurveTableAsync().Returns(model);

        var result = await api.GetNormalCurveTableAsync();

        api.Should().BeAssignableTo<IActorMarketDataFeedQueryApi>();
        result.Success.Should().BeTrue();
        result.Value.Should().BeSameAs(model);
        await db.Received(1).GetNormalCurveTableAsync();
    }

    [Fact]
    public async Task StorageFailureReturnsTheQueryErrorId()
    {
        var (api, db) = CreateApi();
        var exception = new InvalidOperationException("feed unavailable");
        db.GetNormalCurveTableAsync()
            .Returns(_ => Task.FromException<NormalCurveTableReadModel>(exception));

        var result = await api.GetNormalCurveTableAsync();

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(GetNormalCurveTableQuery.ErrorId);
        result.ErrorMessage.Should().Be(exception.Message);
    }

    [Fact]
    public async Task SnapshotFailureStopsTheApiAndRemovesTheStreamId()
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        var snapshot = Substitute.For<IMarketDataSnapshotApi>();
        var streamIds = Substitute.For<IStreamIdCollection>();
        var queryForContract = new FuturesOptionContractReadModel();
        snapshot.StreamIds.Returns(streamIds);
        streamIds.Add("ES-OPTION").Returns(17);
        snapshot.GetFuturesOptionContractAsync(17, queryForContract)
            .Returns(_ => Task.FromException<FuturesOptionContractReadModel?>(
                new InvalidOperationException("snapshot unavailable")));
        var api = new ActorMarketDataFeedQueryApi(
            dbFactory, snapshot, Substitute.For<IBlackboardService>());

        var result = await api.GetFuturesOptionContractAsync("ES-OPTION", queryForContract);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(GetFuturesOptionContractQuery.ErrorId);
        snapshot.Received(1).Start();
        snapshot.Received(1).Stop();
        streamIds.Received(1).Remove(17);
    }

    static (ActorMarketDataFeedQueryApi Api, IMarketDataDbContext Db) CreateApi()
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        var db = Substitute.For<IMarketDataDbContext>();
        dbFactory.MarketDataDb.Returns(db);
        var snapshot = Substitute.For<IMarketDataSnapshotApi>();
        var blackboard = Substitute.For<IBlackboardService>();
        return (new ActorMarketDataFeedQueryApi(dbFactory, snapshot, blackboard), db);
    }
}
