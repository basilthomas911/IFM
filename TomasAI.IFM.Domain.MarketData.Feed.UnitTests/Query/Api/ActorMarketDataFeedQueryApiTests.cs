using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Feed.Query.Api;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.SequenceId;
using ApplicationMarketDataApi = TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.Query.Api;

public class ActorMarketDataFeedQueryApiTests
{
    [Fact]
    public async Task StreamingRequestIdentifierUsesTheSystemSequenceGenerator()
    {
        const SequenceName sequenceName = SequenceName.StreamingRequest_RequestId;
        const long sequenceId = 701;
        var dbFactory = Substitute.For<IDbContextFactory>();
        var generator = Substitute.For<ISequenceIdGenerator>();
        generator
            .GetSequenceIdAsync(sequenceName, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<long>(sequenceId));
        var api = new ActorMarketDataFeedQueryApi(
            dbFactory,
            Substitute.For<ApplicationMarketDataApi>(),
            generator);

        var result = await api.GetStreamingRequestIdAsync();

        result.Success.Should().BeTrue();
        result.Value!.Value.Should().Be((int)sequenceId);
        await generator.Received(1)
            .GetSequenceIdAsync(sequenceName, Arg.Any<CancellationToken>());
    }

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
    public async Task ProviderFailureReturnsTheQueryErrorWithoutStartingAnotherConnection()
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        var marketDataApi = Substitute.For<ApplicationMarketDataApi>();
        var queryForContract = new FuturesOptionContractReadModel();
        marketDataApi.GetFuturesOptionContractAsync("ES-OPTION")
            .Returns(_ => Task.FromException<FuturesOptionContractReadModel?>(
                new InvalidOperationException("provider unavailable")));
        var api = new ActorMarketDataFeedQueryApi(
            dbFactory, marketDataApi, Substitute.For<ISequenceIdGenerator>());

        var result = await api.GetFuturesOptionContractAsync("ES-OPTION", queryForContract);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(GetFuturesOptionContractQuery.ErrorId);
        await marketDataApi.Received(1).GetFuturesOptionContractAsync("ES-OPTION");
    }

    static (ActorMarketDataFeedQueryApi Api, IMarketDataDbContext Db) CreateApi()
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        var db = Substitute.For<IMarketDataDbContext>();
        dbFactory.MarketDataDb.Returns(db);
        var marketDataApi = Substitute.For<ApplicationMarketDataApi>();
        var sequenceIdGenerator = Substitute.For<ISequenceIdGenerator>();
        return (new ActorMarketDataFeedQueryApi(dbFactory, marketDataApi, sequenceIdGenerator), db);
    }
}
