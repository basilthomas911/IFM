using System.Reflection;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.Query.Actor;
using TomasAI.IFM.Domain.Trade.Order.Execution.Query;
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

    [Fact]
    public async Task Order_execution_query_rejects_an_empty_attempt_before_the_projection_read()
    {
        var (_, tradeDb) = CreateProjectionStore();
        var query = new GetOrderExecutionQuery { TradeOrderId = new(1, 2, 3) };

        await Assert.ThrowsAsync<ArgumentException>(() => query.ExecuteAsync(tradeDb, CancellationToken.None));
        Assert.Empty(tradeDb.ReceivedCalls());
    }

    [Fact]
    public async Task Fund_order_fills_actor_replies_with_the_requested_projection_page()
    {
        var context = Substitute.For<IOrderExecutionQueryContext>();
        var (factory, tradeDb) = CreateProjectionStore();
        context.DbFactory.Returns(factory);
        var query = new GetFundOrderFillsQuery
        {
            Subject = new(ActorType.Query, GetFundOrderFillsQuery.Actor, GetFundOrderFillsQuery.Verb, "1.2"),
            PortfolioId = 1,
            FundId = 2,
            ValueDate = new DateOnly(2026, 11, 12),
            AfterOrderId = 100,
            AfterAttemptId = Guid.NewGuid()
        };
        FundOrderFillsRow[] page = [new(101, 4301, Guid.NewGuid(), "Filled", [])];
        tradeDb.GetFundOrderFillsAsync(1, 2, query.ValueDate, query.AfterOrderId, query.AfterAttemptId, Arg.Any<CancellationToken>())
            .Returns(page);

        await GetReceiveMap<IOrderExecutionQueryContext>(typeof(OrderExecutionQueryActor))[query.GetType()](
            context, query, CancellationToken.None);

        await context.Received(1).ReplyAsync(query.Subject.ThreadId, query.Subject.Verb,
            Arg.Is<ServiceResult<FundOrderFillsRow[]>>(reply => reply.Success && reply.Value == page));
    }

    [Fact]
    public void Fund_order_fills_query_rejects_an_invalid_cursor_without_touching_storage()
    {
        var (_, tradeDb) = CreateProjectionStore();
        var query = new GetFundOrderFillsQuery
        {
            PortfolioId = 1, FundId = 2, ValueDate = new DateOnly(2026, 11, 12), AfterAttemptId = Guid.NewGuid()
        };

        Assert.Throws<ArgumentException>(() => { _ = query.ExecuteAsync(tradeDb, CancellationToken.None); });
        Assert.Empty(tradeDb.ReceivedCalls());
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
        query = query with
        {
            PositionId = new(default, Guid.NewGuid()),
            FromUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            ToUtc = new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc),
            LoadAll = true
        };
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
