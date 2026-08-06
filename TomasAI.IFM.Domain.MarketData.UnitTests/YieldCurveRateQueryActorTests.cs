using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.YieldCurveRate.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.UnitTests;

public class YieldCurveRateQueryActorTests
{
    [Fact]
    public async Task ReceiveAsync_WithCancellation_PropagatesTokenToMarketDataDatabaseAndDoesNotReply()
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        var db = Substitute.For<IMarketDataDbContext>();
        dbFactory.MarketDataDb.Returns(db);
        var actor = new TestableYieldCurveRateQueryActor(
            dbFactory, Substitute.For<ILogger<YieldCurveRateQueryActor>>());
        var query = new GetLastYieldCurveRateQuery(initializeDefaults: true);
        query = query with
        {
            Subject = new ActorSubject(
                ActorType.Query,
                GetLastYieldCurveRateQuery.Actor,
                GetLastYieldCurveRateQuery.Verb,
                query.EntityId.Format())
        };
        var context = Substitute.For<IQueryActorContext>();
        using var cancellation = new CancellationTokenSource();
        db.GetLastYieldCurveRateAsync(cancellation.Token)
            .Returns(_ => Task.FromCanceled<YieldCurveRateReadModel?>(cancellation.Token));
        cancellation.Cancel();

        Func<Task> act = () => actor
            .InvokeReceiveAsync(context, query, cancellation.Token)
            .AsTask();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(act);
        await db.Received(1).GetLastYieldCurveRateAsync(cancellation.Token);
        context.DidNotReceiveWithAnyArgs().ReplyAsync(
            default,
            default!,
            default(ServiceResult<YieldCurveRateReadModel?>)!);
    }

    [Fact]
    public async Task ReceiveAsync_GetLastYieldCurveRateQuery_RepliesWithTypedResultAndQueryVerb()
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        var db = Substitute.For<IMarketDataDbContext>();
        dbFactory.MarketDataDb.Returns(db);
        db.GetLastYieldCurveRateAsync().Returns(Task.FromResult<YieldCurveRateReadModel?>(null));

        var actor = new TestableYieldCurveRateQueryActor(
            dbFactory, Substitute.For<ILogger<YieldCurveRateQueryActor>>());
        var query = new GetLastYieldCurveRateQuery(initializeDefaults: true);
        query = query with
        {
            Subject = new ActorSubject(
                ActorType.Query, GetLastYieldCurveRateQuery.Actor,
                GetLastYieldCurveRateQuery.Verb, query.EntityId.Format())
        };
        var context = Substitute.For<IQueryActorContext>();

        await actor.InvokeReceiveAsync(context, query);

        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetLastYieldCurveRateQuery.Verb,
            Arg.Is<ServiceResult<YieldCurveRateReadModel?>>(result =>
                result.Success && result.Value == null));
    }

    sealed class TestableYieldCurveRateQueryActor(
        IDbContextFactory dbFactory,
        ILogger<YieldCurveRateQueryActor> logger)
        : YieldCurveRateQueryActor(dbFactory, logger)
    {
        public ValueTask InvokeReceiveAsync(IQueryActorContext context, IQuery query)
            => base.ReceiveAsync(context, query);

        public ValueTask InvokeReceiveAsync(
            IQueryActorContext context,
            IQuery query,
            CancellationToken cancellationToken)
            => base.ReceiveAsync(context, query, cancellationToken);
    }
}
