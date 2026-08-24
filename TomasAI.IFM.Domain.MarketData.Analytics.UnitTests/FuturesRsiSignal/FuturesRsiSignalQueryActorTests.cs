using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Query;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Query.Actor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesRsiSignal;

public class FuturesRsiSignalQueryActorTests : IClassFixture<MarketDataAnalyticsTestFixture>
{
    readonly MarketDataAnalyticsTestFixture _fixture;

    public FuturesRsiSignalQueryActorTests(MarketDataAnalyticsTestFixture fixture)
    {
        _fixture = fixture;
    }

    public class TestableFuturesRsiSignalQueryActor : FuturesRsiSignalQueryActor
    {
        public TestableFuturesRsiSignalQueryActor(IDbContextFactory dbFactory, ILogger<FuturesRsiSignalQueryActor> logger)
            : base(new FuturesRsiSignalQueryContext(Substitute.For<IActorSupervisor>(), dbFactory, logger))
        {
        }

        public IQuery InvokeParseMessage(IQueryActorContext context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public async ValueTask InvokeReceiveAsync(IQueryActorContext context, IQuery query)
            => await ReceiveAsync(context, query);

        public async ValueTask InvokeReceiveAsync(
            IQueryActorContext context,
            IQuery query,
            CancellationToken cancellationToken)
            => await ReceiveAsync(context, query, cancellationToken);

        public async ValueTask InvokeOnExceptionAsync(IQueryActorContext context, ActorThreadId threadId, IQuery query, string verb, Exception ex)
            => await OnExceptionAsync(context, threadId, query, verb, ex);


    }

    [Fact]
    public async Task ReceiveAsync_WithCancellation_PropagatesTokenAndDoesNotReply()
    {
        var marketDataDb = Substitute.For<IMarketDataDbContext>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.MarketDataDb.Returns(marketDataDb);
        var actor = _fixture.CreateRsiQueryActor(dbFactory: dbFactory);
        var query = new GetFuturesRsiSignalQuery(
            SampleData.ContractId,
            SampleData.ValueDate,
            SampleData.TimePeriod,
            SampleData.PeriodLength) with
        {
            Subject = new ActorSubject(
                ActorType.Query,
                GetFuturesRsiSignalQuery.Actor,
                GetFuturesRsiSignalQuery.Verb,
                new GetFuturesRsiSignalParameter(
                    SampleData.ContractId,
                    SampleData.ValueDate,
                    SampleData.TimePeriod,
                    SampleData.PeriodLength).Format())
        };
        var context = Substitute.For<IQueryActorContext>();
        using var cancellation = new CancellationTokenSource();
        marketDataDb.GetLastFuturesRsiSignalAsync(
                query.ContractId,
                query.ValueDate,
                query.TimePeriod,
                query.PeriodLength,
                cancellation.Token)
            .Returns(_ => Task.FromCanceled<FuturesRsiSignalReadModel?>(cancellation.Token));
        cancellation.Cancel();

        Func<Task> act = () => actor
            .InvokeReceiveAsync(context, query, cancellation.Token)
            .AsTask();

        await act.Should().ThrowAsync<OperationCanceledException>();
        await marketDataDb.Received(1).GetLastFuturesRsiSignalAsync(
            query.ContractId,
            query.ValueDate,
            query.TimePeriod,
            query.PeriodLength,
            cancellation.Token);
        context.DidNotReceiveWithAnyArgs().ReplyAsync(
            default,
            default!,
            default(ServiceResult<FuturesRsiSignalReadModel?>)!);
    }


}
