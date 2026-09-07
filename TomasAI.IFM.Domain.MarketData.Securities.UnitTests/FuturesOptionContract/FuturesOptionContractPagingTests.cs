using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Query;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Securities.UnitTests.FuturesOptionContract;

public sealed class FuturesOptionContractPagingTests
{
    [Theory]
    [InlineData("", 200)]
    [InlineData(" ", 200)]
    [InlineData("ES", 0)]
    [InlineData("ES", 1001)]
    public async Task Invalid_requests_do_not_reach_storage(string symbol, int pageSize)
    {
        var factory = Substitute.For<IDbContextFactory>();
        var db = Substitute.For<ISecuritiesDbContext>();
        factory.SecuritiesDb.Returns(db);
        var query = new GetFuturesOptionContractsPageQuery(new(symbol, pageSize));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => query.GetFuturesOptionContractsPageAsync(factory));
        Assert.Empty(db.ReceivedCalls());
    }

    [Fact]
    public async Task Actor_routes_page_request_and_preserves_continuation_and_cancellation()
    {
        var factory = Substitute.For<IDbContextFactory>();
        var db = Substitute.For<ISecuritiesDbContext>();
        factory.SecuritiesDb.Returns(db);
        using var cancellation = new CancellationTokenSource();
        var request = new GetFuturesOptionContractsPageParameter("ES", 200, "cursor");
        var page = new FuturesOptionContractPageReadModel([], "next");
        db.GetFuturesOptionContractsPageAsync(request, cancellation.Token).Returns(page);
        var actor = new FuturesOptionContractQueryActorTests.TestableFuturesOptionContractQueryActor(
            factory, Substitute.For<ILogger<FuturesOptionContractQueryActor>>());
        var query = new GetFuturesOptionContractsPageQuery(request)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesOptionContractsPageQuery.Actor,
                GetFuturesOptionContractsPageQuery.Verb, request.Format())
        };
        await actor.InvokeReceiveAsync(actor.FuturesOptionContext, query, cancellation.Token);
        await db.Received(1).GetFuturesOptionContractsPageAsync(request, cancellation.Token);
        await actor.FuturesOptionContext.Received(1).ReplyAsync(query.Subject.ThreadId,
            GetFuturesOptionContractsPageQuery.Verb,
            Arg.Is<ServiceResult<FuturesOptionContractPageReadModel>>(r => r.Value == page));
        await db.DidNotReceive().GetFuturesOptionContractsAsync(Arg.Any<string>());
    }

    [Fact]
    public void Query_and_page_roundtrip_with_the_application_messagepack_serializer()
    {
        var serializer = new TomasAI.IFM.Framework.Serialization.MessagePackBinarySerializer();
        var request = new GetFuturesOptionContractsPageParameter("ES", 200, "opaque+/=");
        var query = new GetFuturesOptionContractsPageQuery(request)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesOptionContractsPageQuery.Actor,
                GetFuturesOptionContractsPageQuery.Verb, request.Format())
        };
        var restored = serializer.Deserialize<GetFuturesOptionContractsPageQuery>(serializer.Serialize(query)!);
        Assert.Equal(request, restored!.Request);
        Assert.Equal("ES", restored.EntityId.Format());
        var page = new FuturesOptionContractPageReadModel([], "next");
        var restoredPage = serializer.Deserialize<FuturesOptionContractPageReadModel>(serializer.Serialize(page)!);
        Assert.Empty(restoredPage!.Items);
        Assert.Equal("next", restoredPage.ContinuationToken);
    }

    [Fact]
    public void Http_query_parameters_escape_continuation_token_and_symbol()
    {
        var request = new GetFuturesOptionContractsPageParameter("A+B", 200, "x+/=.y==");
        Assert.Equal("symbol=A%2BB&pageSize=200&continuationToken=x%2B%2F%3D.y%3D%3D", request.QueryParams);
    }
}
