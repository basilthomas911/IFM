using System.Reflection;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.Query.Actor;
using TomasAI.IFM.Domain.Trade.Order.Execution.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared.Order.Execution;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Architecture;

public sealed class TradeProjectionQueryActorTests
{
    [Fact]
    public async Task Order_execution_actor_replies_once_after_the_projection_read()
    {
        var context = Substitute.For<IOrderExecutionQueryContext>();
        var (factory, tradeDb) = CreateProjectionStore();
        context.DbFactory.Returns(factory);
        var query = new GetOrderExecutionQuery
        {
            Subject = new(ActorType.Query, GetOrderExecutionQuery.Actor, GetOrderExecutionQuery.Verb, "1.2.3"),
            TradeOrderId = new(1, 2, 3),
            ExecutionAttemptId = Guid.NewGuid()
        };
        var projection = new OrderExecutionDefinition();
        var read = new TaskCompletionSource<OrderExecutionDefinition?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        tradeDb.GetOrderExecutionAsync(query.TradeOrderId, query.ExecutionAttemptId, cancellation.Token).Returns(read.Task);
        var receive = GetReceiveMap<IOrderExecutionQueryContext>(typeof(OrderExecutionQueryActor));

        var dispatch = receive[query.GetType()](context, query, cancellation.Token);
        await context.DidNotReceiveWithAnyArgs().ReplyAsync(default, default!, default(ServiceResult<OrderExecutionDefinition?>)!);
        read.SetResult(projection);
        await dispatch;

        await context.ReceivedWithAnyArgs(1).ReplyAsync(default, default!, default(ServiceResult<OrderExecutionDefinition?>)!);
        await context.Received(1).ReplyAsync(query.Subject.ThreadId, query.Subject.Verb,
            Arg.Is<ServiceResult<OrderExecutionDefinition?>>(reply => reply.Success && reply.Value == projection));
    }

    [Theory]
    [InlineData(TradeStrategyKind.IronCondor)]
    [InlineData(TradeStrategyKind.VerticalSpread)]
    public async Task Option_history_actor_replies_with_only_the_requested_position_and_strategy(TradeStrategyKind kind)
    {
        var context = Substitute.For<IFuturesOptionPositionQueryContext>();
        var (factory, tradeDb) = CreateProjectionStore();
        context.DbFactory.Returns(factory);
        PositionHistoryQuery query = kind == TradeStrategyKind.IronCondor
            ? new GetIronCondorOptionTradePositionHistoryQuery
            {
                Subject = new(ActorType.Query, FuturesOptionPositionQueryActor.ActorName, GetIronCondorOptionTradePositionHistoryQuery.Verb, "position")
            }
            : new GetVerticalSpreadOptionTradePositionHistoryQuery
            {
                Subject = new(ActorType.Query, FuturesOptionPositionQueryActor.ActorName, GetVerticalSpreadOptionTradePositionHistoryQuery.Verb, "position")
            };
        query = query with { PositionId = new(default, Guid.NewGuid()), LoadAll = true };
        var expected = new StrategyPositionSnapshot { Id = query.PositionId, StrategyKind = kind };
        var wrongPosition = expected with { Id = new(default, Guid.NewGuid()) };
        var wrongStrategy = expected with { StrategyKind = kind == TradeStrategyKind.IronCondor ? TradeStrategyKind.VerticalSpread : TradeStrategyKind.IronCondor };
        byte[] cursor = [1];
        using var cancellation = new CancellationTokenSource();
        tradeDb.GetStrategyPositionHistoryAsync(query.PositionId.PositionId, query.FromUtc, query.ToUtc, query.PageSize, null, cancellation.Token)
            .Returns(new QueryPage<StrategyPositionSnapshot>([wrongStrategy], cursor));
        tradeDb.GetStrategyPositionHistoryAsync(query.PositionId.PositionId, query.FromUtc, query.ToUtc, query.PageSize, cursor, cancellation.Token)
            .Returns(new QueryPage<StrategyPositionSnapshot>([wrongPosition, expected], null));
        var receive = GetReceiveMap<IFuturesOptionPositionQueryContext>(typeof(FuturesOptionPositionQueryActor));

        await receive[query.GetType()](context, query, cancellation.Token);

        await context.ReceivedWithAnyArgs(1).ReplyAsync(default, default!, default(ServiceResult<StrategyPositionSnapshot[]>)!);
        await context.Received(1).ReplyAsync(query.Subject.ThreadId, query.Subject.Verb,
            Arg.Is<ServiceResult<StrategyPositionSnapshot[]>>(reply => reply.Success && reply.Value!.SequenceEqual(new[] { expected })));
    }

    static (IDbContextFactory Factory, ITradeDbContext TradeDb) CreateProjectionStore()
    {
        var factory = Substitute.For<IDbContextFactory>();
        var tradeDb = Substitute.For<ITradeDbContext>();
        factory.TradeDb.Returns(tradeDb);
        return (factory, tradeDb);
    }

    static IReadOnlyDictionary<Type, Func<TContext, IQuery, CancellationToken, ValueTask>> GetReceiveMap<TContext>(Type actor)
        => (IReadOnlyDictionary<Type, Func<TContext, IQuery, CancellationToken, ValueTask>>)actor
            .GetField("_receiveMap", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
}
