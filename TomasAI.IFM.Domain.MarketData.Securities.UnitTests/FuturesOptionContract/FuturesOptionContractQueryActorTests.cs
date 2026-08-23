using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.State;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Query;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Query.Actor;

namespace TomasAI.IFM.Domain.MarketData.Securities.UnitTests.FuturesOptionContract;

public class FuturesOptionContractQueryActorTests : IClassFixture<SecuritiesFixture>
{
    readonly SecuritiesFixture _fixture;

    public FuturesOptionContractQueryActorTests(SecuritiesFixture fixture)
    {
        _fixture = fixture;
    }

    // Test helper to expose protected ParseMessage and ReceiveAsync for unit testing.
    public class TestableFuturesOptionContractQueryActor : FuturesOptionContractQueryActor
    {
        public TestableFuturesOptionContractQueryActor(
            IDbContextFactory dbFactory,
            ILogger<FuturesOptionContractQueryActor> logger)
            : this(CreateContext(dbFactory, logger))
        {
        }

        TestableFuturesOptionContractQueryActor(IFuturesOptionContractQueryContext context) : base(context)
            => FuturesOptionContext = context;

        static IFuturesOptionContractQueryContext CreateContext(
            IDbContextFactory dbFactory, ILogger<FuturesOptionContractQueryActor> logger)
        {
            var context = Substitute.For<IFuturesOptionContractQueryContext>();
            context.ActorId.Returns(new ActorMailboxId(ActorType.Query, FuturesOptionContractQueryActor.ActorName));
            context.DbFactory.Returns(dbFactory);
            context.Logger.Returns(logger);
            return context;
        }

        public IFuturesOptionContractQueryContext FuturesOptionContext { get; }

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
        var securitiesDb = Substitute.For<ISecuritiesDbContext>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.SecuritiesDb.Returns(securitiesDb);
        var actor = _fixture.CreateActor(
            dbFactory,
            Substitute.For<ILogger<FuturesOptionContractQueryActor>>());
        var query = new GetFuturesOptionContractQuery(SampleData.FuturesOptionContract1.ContractId) with
        {
            Subject = new ActorSubject(
                ActorType.Query,
                GetFuturesOptionContractQuery.Actor,
                GetFuturesOptionContractQuery.Verb,
                SampleData.FuturesOptionContract1.ContractId)
        };
        var context = actor.FuturesOptionContext;
        using var cancellation = new CancellationTokenSource();
        securitiesDb.GetFuturesOptionContractAsync(query.ContractId, cancellation.Token)
            .Returns(_ => Task.FromCanceled<FuturesOptionContractReadModel?>(cancellation.Token));
        cancellation.Cancel();

        Func<Task> act = () => actor
            .InvokeReceiveAsync(context, query, cancellation.Token)
            .AsTask();

        await act.Should().ThrowAsync<OperationCanceledException>();
        await securitiesDb.Received(1)
            .GetFuturesOptionContractAsync(query.ContractId, cancellation.Token);
        context.DidNotReceiveWithAnyArgs().ReplyAsync(
            default,
            default!,
            default(ServiceResult<FuturesOptionContractReadModel?>)!);
    }


}

