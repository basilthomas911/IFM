using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.State;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Query;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Query.Actor;

namespace TomasAI.IFM.Domain.MarketData.Securities.UnitTests.FuturesContract;

public class FuturesContractQueryActorTests : IClassFixture<SecuritiesFixture>
{
    readonly SecuritiesFixture _fixture;

    public FuturesContractQueryActorTests(SecuritiesFixture fixture)
    {
        _fixture = fixture;
    }

    // Test helper to expose protected ParseMessage and ReceiveAsync for unit testing.
    public class TestableFuturesContractQueryActor : FuturesContractQueryActor
    {
        public TestableFuturesContractQueryActor(IDbContextFactory dbFactory, ILogger<FuturesContractQueryActor> logger)
            : this(CreateContext(dbFactory, logger))
        {
        }

        TestableFuturesContractQueryActor(IFuturesContractQueryContext context) : base(context)
            => FuturesContext = context;

        static IFuturesContractQueryContext CreateContext(IDbContextFactory dbFactory, ILogger<FuturesContractQueryActor> logger)
        {
            var context = Substitute.For<IFuturesContractQueryContext>();
            context.ActorId.Returns(new ActorMailboxId(ActorType.Query, FuturesContractQueryActor.ActorName));
            context.DbFactory.Returns(dbFactory);
            context.Logger.Returns(logger);
            return context;
        }

        public IFuturesContractQueryContext FuturesContext { get; }

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
            Substitute.For<ILogger<FuturesContractQueryActor>>());
        var query = new GetFuturesContractQuery(SampleData.FuturesContract1.ContractId) with
        {
            Subject = new ActorSubject(
                ActorType.Query,
                GetFuturesContractQuery.Actor,
                GetFuturesContractQuery.Verb,
                SampleData.FuturesContract1.ContractId)
        };
        var context = actor.FuturesContext;
        using var cancellation = new CancellationTokenSource();
        securitiesDb.GetFuturesContractAsync(query.ContractId, cancellation.Token)
            .Returns(_ => Task.FromCanceled<FuturesContractV2ReadModel?>(cancellation.Token));
        cancellation.Cancel();

        Func<Task> act = () => actor
            .InvokeReceiveAsync(context, query, cancellation.Token)
            .AsTask();

        await act.Should().ThrowAsync<OperationCanceledException>();
        await securitiesDb.Received(1)
            .GetFuturesContractAsync(query.ContractId, cancellation.Token);
        context.DidNotReceiveWithAnyArgs().ReplyAsync(
            default,
            default!,
            default(ServiceResult<FuturesContractV2ReadModel?>)!);
    }

}


