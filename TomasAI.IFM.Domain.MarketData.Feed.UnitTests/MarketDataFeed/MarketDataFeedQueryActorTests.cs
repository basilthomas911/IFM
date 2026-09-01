using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Feed.Query;
using TomasAI.IFM.Domain.MarketData.Feed.Query.Actor;
using TomasAI.IFM.Framework.SequenceId;
using ApplicationMarketDataApi = TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.MarketDataFeed;

public class MarketDataFeedQueryActorTests : IClassFixture<MarketDataFeedTestFixture>
{
    readonly MarketDataFeedTestFixture _fixture;

    public MarketDataFeedQueryActorTests(MarketDataFeedTestFixture fixture)
    {
        _fixture = fixture;
    }

    public class TestableMarketDataFeedQueryActor : MarketDataFeedQueryActor
    {
        public IMarketDataFeedQueryContext Context { get; }

        public TestableMarketDataFeedQueryActor(
            ApplicationMarketDataApi marketDataApi,
            ISequenceIdGenerator sequenceIdGenerator,
            IDbContextFactory dbFactory,
            ILogger<MarketDataFeedQueryActor> logger)
            : this(TypedActorContextFactory.Query(dbFactory, logger)) { }

        public TestableMarketDataFeedQueryActor(IMarketDataFeedQueryContext context)
            : base(context) => Context = context;

        public IQuery InvokeParseMessage(IQueryActorContext<MarketDataFeedQueryActor> context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public async ValueTask InvokeReceiveAsync(IQueryActorContext<MarketDataFeedQueryActor> context, IQuery query)
            => await ReceiveAsync(context, query);

        public async ValueTask InvokeOnExceptionAsync(IQueryActorContext<MarketDataFeedQueryActor> context, ActorThreadId threadId, IQuery query, string verb, Exception ex)
            => await OnExceptionAsync(context, threadId, query, verb, ex);


    }

    [Fact]
    [Trait("TestType", "Integration")]
    public async Task RuntimeStatusQuery_ReturnsTypedApplicationFeedState()
    {
        var expected = new MarketDataFeedRuntimeStatusReadModel
        {
            IsRunning = true,
            ActiveValueDate = new DateOnly(2026, 9, 1),
            ObservedAtUtc = new DateTimeOffset(2026, 8, 31, 22, 0, 0, TimeSpan.Zero)
        };
        var marketDataApi = Substitute.For<ApplicationMarketDataApi>();
        marketDataApi.GetRuntimeStatus().Returns(expected);
        var context = Substitute.For<IMarketDataFeedQueryContext>();
        context.MarketDataApi.Returns(marketDataApi);
        context.DbFactory.Returns(Substitute.For<IDbContextFactory>());
        context.SequenceIdGenerator.Returns(Substitute.For<ISequenceIdGenerator>());
        context.Logger.Returns(Substitute.For<ILogger<MarketDataFeedQueryActor>>());
        context.ActorId.Returns(new ActorMailboxId(ActorType.Query, MarketDataFeedQueryActor.ActorName));
        var actor = new TestableMarketDataFeedQueryActor(context);
        var query = new GetMarketDataFeedRuntimeStatusQuery
        {
            Subject = new ActorSubject(
                ActorType.Query,
                GetMarketDataFeedRuntimeStatusQuery.Actor,
                GetMarketDataFeedRuntimeStatusQuery.Verb,
                "runtime-status")
        };

        await actor.InvokeReceiveAsync(context, query);

        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetMarketDataFeedRuntimeStatusQuery.Verb,
            Arg.Is<ServiceResult<MarketDataFeedRuntimeStatusReadModel>>(result =>
                result.Success && ReferenceEquals(result.Value, expected)));
    }

 
}
