using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Domain.Trade.Option.Command.State;
using TomasAI.IFM.Domain.Trade.Option.Query;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Domain.Trade.Shared.QueryParameters;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Option.Query.Actor;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Option;

public class OptionTradeQueryActorTests : IClassFixture<TradeFixture>
{
    readonly TradeFixture _fixture;

    public OptionTradeQueryActorTests(TradeFixture fixture)
    {
        _fixture = fixture;
    }

    public class TestableOptionTradeQueryActor(
        IDbContextFactory dbFactory,
        IBlackboardService blackboardService,
        ILogger<OptionTradeQueryActor> logger)
        : OptionTradeQueryActor(new OptionTradeQueryContext(Substitute.For<IActorSupervisor>(), dbFactory, blackboardService, logger))
    {
        public IQuery InvokeParseMessage(IQueryActorContext<OptionTradeQueryActor> context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public async ValueTask InvokeReceiveAsync(IQueryActorContext<OptionTradeQueryActor> context, IQuery query)
            => await ReceiveAsync(context, query);

        public async ValueTask InvokeReceiveAsync(
            IQueryActorContext<OptionTradeQueryActor> context,
            IQuery query,
            CancellationToken cancellationToken)
            => await ReceiveAsync(context, query, cancellationToken);

        public async ValueTask InvokeOnExceptionAsync(IQueryActorContext<OptionTradeQueryActor> context, ActorThreadId threadId, IQuery query, string verb, Exception ex)
            => await OnExceptionAsync(context, threadId, query, verb, ex);
    }

    [Fact]
    public async Task ReceiveAsync_WithCancellation_PropagatesTokenAndDoesNotReply()
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        var tradeDb = Substitute.For<ITradeDbContext>();
        dbFactory.TradeDb.Returns(tradeDb);
        var actor = _fixture.CreateQueryActor(dbFactory);
        var query = new GetOptionTradeQuery(100, 1);
        var context = Substitute.For<IQueryActorContext<OptionTradeQueryActor>>();
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        tradeDb.GetOptionTradeAsync(100, 1, cancellation.Token)
            .Returns(async _ =>
            {
                started.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellation.Token);
                return null;
            });

        var operation = actor
            .InvokeReceiveAsync(context, query, cancellation.Token)
            .AsTask();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(1));
        cancellation.Cancel();

        Func<Task> act = async () => await operation;

        await act.Should().ThrowAsync<OperationCanceledException>();
        await tradeDb.Received(1).GetOptionTradeAsync(100, 1, cancellation.Token);
        context.DidNotReceiveWithAnyArgs().ReplyAsync(
            default,
            default!,
            default(ServiceResult<OptionTradeReadModel?>)!);
    }

}
