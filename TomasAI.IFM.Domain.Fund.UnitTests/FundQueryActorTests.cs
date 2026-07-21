using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using System.Reflection;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ScyllaDb.FundDb;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Fund.Shared.Queries;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Fund.Query.Actor;

namespace TomasAI.IFM.Domain.Fund.UnitTests;

public class FundQueryActorTests : IClassFixture<FundTestFixture>
{
    readonly FundTestFixture _fixture;

    public FundQueryActorTests(FundTestFixture fixture)
    {
        _fixture = fixture;
    }

    // Test helper to expose protected ParseMessage and ReceiveAsync for unit testing.
    public class TestableFundQueryActor : FundQueryActor
    {
        public TestableFundQueryActor(IDbContextFactory dbFactory, ILogger<FundQueryActor> logger)
            : base(dbFactory,logger)
        {
        }

        public IQuery InvokeParseMessage(IQueryActorContext context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public async ValueTask InvokeReceiveAsync(IQueryActorContext context, IQuery query)
            => await ReceiveAsync(context, query);

        public async ValueTask InvokeOnExceptionAsync(IQueryActorContext context, ActorThreadId threadId, IQuery query, string verb, Exception ex)
            => await OnExceptionAsync(context, threadId, query, verb, ex);

    }


    [Fact]
    public async Task OnExceptionAsync_RepliesWithFailure_ForSupportedAndUnsupportedQueries()
    {
        var logger = Substitute.For<ILogger<FundQueryActor>>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        var actor = _fixture.CreateActor(dbFactory, logger);

        var context = Substitute.For<IQueryActorContext>();
        // ensure ReplyAsync doesn't throw when called
        context.ReplyAsync(Arg.Any<ActorThreadId>(), Arg.Any<string>(), Arg.Any<ServiceResult<object>>()).Returns(ValueTask.CompletedTask);

        var now = DateOnly.FromDateTime(DateTime.UtcNow);
        var cases = new (IQuery Query, Type ResultType)[]
        {
            (new GetClosingFundBalanceQuery(SampleData.Fund.FundId, now), typeof(FundBalanceReadModel)),
            (new GetFundBalanceQuery(SampleData.Fund.FundId), typeof(FundBalanceReadModel)),
            (new GetFundDrawdownBalancesQuery(SampleData.Fund.FundId, now.AddDays(-7), now), typeof(FundDrawdownBalancesReadModel)),
            (new GetFundIdFromOrderIdQuery(123), typeof(ScalarReadModel<int>)),
            (new GetFundOrdersQuery(), typeof(FundOrderReadModel[])),
            (new GetFundOrderTradesQuery(), typeof(FundOrderTradeReadModel[])),
            (new GetFundPnlReportQuery(SampleData.Fund.FundId, now.AddDays(-7), now), typeof(FundPnlReportReadModel[])),
            (new GetFundsQuery(), typeof(FundReadModel[])),
            (new GetFundWinLossRatioQuery(SampleData.Fund.FundId, now.AddDays(-7), now), typeof(FundWinLossRatioReadModel)),
            (new GetOpeningFundBalanceQuery(SampleData.Fund.FundId, now), typeof(FundBalanceReadModel)),
            (new GetFundMaxProfitGeneratedQuery(SampleData.Fund.FundId, now), typeof(ActorEntityId))
        };

        foreach (var (query, resultType) in cases)
        {
            var threadId = new ActorThreadId(ActorType.Query, FundQueryActor.ActorName, (query as dynamic).EntityId.Format());
            // determine verb via reflection on the query type
            var qType = query.GetType();
            var verbField = qType.GetField("Verb", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            var verb = verbField != null ? (string)verbField.GetValue(null)! : qType.Name;
            var ex = new Exception("boom");
            var expectedErrorCode = query.ErrorCode;

            await actor.InvokeOnExceptionAsync(context, threadId, query, verb, ex);

            // verify ReplyAsync was called with correct generic and content
            if (resultType == typeof(ActorEntityId))
            {
                await context.Received(1).ReplyAsync(Arg.Is<ActorThreadId>(id => id == threadId), Arg.Is<string>(v => v == verb), Arg.Is<ServiceResult<ActorEntityId>>(r => !r.Success && r.ErrorCode == 9999 && r.ErrorMessage == ex.Message));
            }
            else
            {
                if (resultType == typeof(FundBalanceReadModel))
                {
                    await context.Received(1).ReplyAsync(Arg.Is<ActorThreadId>(id => id == threadId), Arg.Is<string>(v => v == verb), Arg.Is<ServiceResult<FundBalanceReadModel>>(r => !r.Success && r.ErrorCode == expectedErrorCode && r.ErrorMessage == ex.Message));
                }
                else if (resultType == typeof(FundDrawdownBalancesReadModel))
                {
                    await context.Received(1).ReplyAsync(Arg.Is<ActorThreadId>(id => id == threadId), Arg.Is<string>(v => v == verb), Arg.Is<ServiceResult<FundDrawdownBalancesReadModel>>(r => !r.Success && r.ErrorCode == expectedErrorCode && r.ErrorMessage == ex.Message));
                }
                else if (resultType == typeof(ScalarReadModel<int>))
                {
                    await context.Received(1).ReplyAsync(Arg.Is<ActorThreadId>(id => id == threadId), Arg.Is<string>(v => v == verb), Arg.Is<ServiceResult<ScalarReadModel<int>>>(r => !r.Success && r.ErrorCode == expectedErrorCode && r.ErrorMessage == ex.Message));
                }
                else if (resultType == typeof(FundOrderReadModel[]))
                {
                    await context.Received(1).ReplyAsync(Arg.Is<ActorThreadId>(id => id == threadId), Arg.Is<string>(v => v == verb), Arg.Is<ServiceResult<FundOrderReadModel[]>>(r => !r.Success && r.ErrorCode == expectedErrorCode && r.ErrorMessage == ex.Message));
                }
                else if (resultType == typeof(FundOrderTradeReadModel[]))
                {
                    await context.Received(1).ReplyAsync(Arg.Is<ActorThreadId>(id => id == threadId), Arg.Is<string>(v => v == verb), Arg.Is<ServiceResult<FundOrderTradeReadModel[]>>(r => !r.Success && r.ErrorCode == expectedErrorCode && r.ErrorMessage == ex.Message));
                }
                else if (resultType == typeof(FundPnlReportReadModel[]))
                {
                    await context.Received(1).ReplyAsync(Arg.Is<ActorThreadId>(id => id == threadId), Arg.Is<string>(v => v == verb), Arg.Is<ServiceResult<FundPnlReportReadModel[]>>(r => !r.Success && r.ErrorCode == expectedErrorCode && r.ErrorMessage == ex.Message));
                }
                else if (resultType == typeof(FundReadModel[]))
                {
                    await context.Received(1).ReplyAsync(Arg.Is<ActorThreadId>(id => id == threadId), Arg.Is<string>(v => v == verb), Arg.Is<ServiceResult<FundReadModel[]>>(r => !r.Success && r.ErrorCode == expectedErrorCode && r.ErrorMessage == ex.Message));
                }
                else if (resultType == typeof(FundWinLossRatioReadModel))
                {
                    await context.Received(1).ReplyAsync<FundWinLossRatioReadModel>(Arg.Is<ActorThreadId>(id => id == threadId), Arg.Is<string>(v => v == verb), Arg.Is<ServiceResult<FundWinLossRatioReadModel>>(r => !r.Success && r.ErrorCode == expectedErrorCode && r.ErrorMessage == ex.Message));
                }
                else
                {
                    // fallback - ensure a reply was sent (generic check)
                    context.Received(1).ReplyAsync(Arg.Is<ActorThreadId>(id => id == threadId), Arg.Is<string>(v => v == verb), Arg.Any<ServiceResult<object>>());
                }
            }

            context.ClearReceivedCalls();
        }
    }

    [Fact]
    public async Task OnExceptionAsync_ThrowsWhenContextIsNull()
    {
        var logger = Substitute.For<ILogger<FundQueryActor>>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        var actor = _fixture.CreateActor(dbFactory, logger);

        var q = new GetFundBalanceQuery(SampleData.Fund.FundId);
        var threadId = new ActorThreadId(ActorType.Query, FundQueryActor.ActorName, q.EntityId.Format());
        var ex = new Exception("boom");

        Func<Task> act = async () => await actor.InvokeOnExceptionAsync(null!, threadId, q, GetFundBalanceQuery.Verb, ex);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnExceptionAsync_ThrowsWhenThreadIdInvalid()
    {
        var logger = Substitute.For<ILogger<FundQueryActor>>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        var actor = _fixture.CreateActor(dbFactory, logger);

        var context = Substitute.For<IQueryActorContext>();
        var q = new GetFundBalanceQuery(SampleData.Fund.FundId);
        var invalidThreadId = default(ActorThreadId);
        var ex = new Exception("boom");

        Func<Task> act = async () => await actor.InvokeOnExceptionAsync(context, invalidThreadId, q, GetFundBalanceQuery.Verb, ex);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnExceptionAsync_ThrowsWhenQueryIsNull()
    {
        var logger = Substitute.For<ILogger<FundQueryActor>>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        var actor = _fixture.CreateActor(dbFactory, logger);

        var context = Substitute.For<IQueryActorContext>();
        var threadId = new ActorThreadId(ActorType.Query, FundQueryActor.ActorName, "1");
        var ex = new Exception("boom");

        Func<Task> act = async () => await actor.InvokeOnExceptionAsync(context, threadId, null!, "verb", ex);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnExceptionAsync_ThrowsWhenVerbIsNullOrWhitespace()
    {
        var logger = Substitute.For<ILogger<FundQueryActor>>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        var actor = _fixture.CreateActor(dbFactory, logger);

        var context = Substitute.For<IQueryActorContext>();
        var q = new GetFundBalanceQuery(SampleData.Fund.FundId);
        var threadId = new ActorThreadId(ActorType.Query, FundQueryActor.ActorName, q.EntityId.Format());
        var ex = new Exception("boom");

        Func<Task> act1 = async () => await actor.InvokeOnExceptionAsync(context, threadId, q, null!, ex);
        await act1.Should().ThrowAsync<ArgumentNullException>();

        Func<Task> act2 = async () => await actor.InvokeOnExceptionAsync(context, threadId, q, string.Empty, ex);
        await act2.Should().ThrowAsync<ArgumentNullException>();
    }


    #region ParseMessage Tests

    [Theory]
    [InlineData(typeof(GetClosingFundBalanceQuery), "GetClosingFundBalance")]
    [InlineData(typeof(GetFundBalanceQuery), "GetFundBalance")]
    [InlineData(typeof(GetFundDrawdownBalancesQuery), "GetFundDrawdownBalances")]
    [InlineData(typeof(GetFundIdFromOrderIdQuery), "GetFundIdFromOrderId")]
    [InlineData(typeof(GetFundOrdersQuery), "GetFundOrders")]
    [InlineData(typeof(GetFundOrderTradesQuery), "GetFundOrderTrades")]
    [InlineData(typeof(GetFundPnlReportQuery), "GetFundPnlReport")]
    [InlineData(typeof(GetFundsQuery), "GetFunds")]
    [InlineData(typeof(GetFundWinLossRatioQuery), "GetFundWinLossRatio")]
    [InlineData(typeof(GetOpeningFundBalanceQuery), "GetOpeningFundBalance")]
    [InlineData(typeof(GetFundMaxProfitGeneratedQuery), "GetFundMaxProfitGenerated")]
    public void ParseMessage_ShouldDeserializeSupportedQueries_AndSetMessageInfo(Type queryType, string expectedVerb)
    {
        _fixture.DataSerializer.Should().NotBeNull();
        _fixture.MsgSerializer.Should().NotBeNull();

        var logger = Substitute.For<ILogger<FundQueryActor>>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        var actor = new TestableFundQueryActor(dbFactory, logger);

        // create query instance as concrete type (avoid serializing via interface type)
        var queryObj = Activator.CreateInstance(queryType)!;
        // set subject thread id and verb
        var subject = new ActorSubject(ActorType.Query, FundQueryActor.ActorName, expectedVerb, "1");
        // assign subject via reflection for IQuery implementations that expose Subject init property
        var subjectProp = queryType.GetProperty("Subject");
        subjectProp?.SetValue(queryObj, subject);

        // serialize using the concrete query type to avoid MessagePack trying to serialize the interface
        var serializer = ActorExtensions.DataSerializer;
        var serializeMethod = serializer.GetType().GetMethod("Serialize")!;
        var genericSerialize = serializeMethod.MakeGenericMethod(queryType);
        var payload = (byte[])genericSerialize.Invoke(serializer, new object[] { queryObj })!;
        var natsMsg = new NatsMsg<byte[]>(subject.ToString(), string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<IQueryActorContext>();
        context.SetMessageInfo(Arg.Any<ActorThreadId>(), Arg.Any<string>(), Arg.Any<ActorMessageInfo>()).Returns(true);

        // Act
        actor.InvokeParseMessage(context, natsMsg);

        // Assert
        context.Received(1).SetMessageInfo(
            Arg.Is<ActorThreadId>(id => id == subject.ThreadId),
            Arg.Is<string>(v => v == expectedVerb),
            Arg.Is<ActorMessageInfo>(info => info.ActorMessage.Subject == subject.ToString())
        );
    }

    [Fact]
    public void ParseMessage_ShouldThrowArgumentNullException_WhenContextIsNull()
    {
        var logger = Substitute.For<ILogger<FundQueryActor>>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        var actor = new TestableFundQueryActor(dbFactory, logger);

        var q = new GetFundBalanceQuery(1);
        q = q with { Subject = new ActorSubject(ActorType.Query, FundQueryActor.ActorName, GetFundBalanceQuery.Verb, q.EntityId.Format()) };
        var payload = ActorExtensions.DataSerializer.Serialize(q);
        var natsMsg = new NatsMsg<byte[]>(q.Subject.ToString(), string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        Action act = () => actor.InvokeParseMessage(null!, natsMsg);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ParseMessage_ShouldThrowInvalidOperationException_WhenSubjectInvalid()
    {
        var logger = Substitute.For<ILogger<FundQueryActor>>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        var actor = new TestableFundQueryActor(dbFactory, logger);

        var invalidSubject = "Invalid.Subject.Format";
        var natsMsg = new NatsMsg<byte[]>(invalidSubject, string.Empty, 0, default!, Array.Empty<byte>(), default!, NatsMsgFlags.None);
        var context = Substitute.For<IQueryActorContext>();

        Action act = () => actor.InvokeParseMessage(context, natsMsg);
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Remaining ReceiveAsync Tests

    [Fact]
    public async Task ReceiveAsync_GetClosingFundBalanceQuery_RepliesWithResult()
    {
        var logger = Substitute.For<ILogger<FundQueryActor>>();
        var fundDbContext = Substitute.For<IFundDbContext>();
        fundDbContext.GetClosingFundBalanceAsync(Arg.Any<int>(), Arg.Any<DateOnly>())
            .Returns(Task.FromResult(123.45M));
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.FundDb
            .Returns(fundDbContext);

        var actor = _fixture.CreateActor(dbFactory, logger);
        var valueDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var q = new GetClosingFundBalanceQuery(SampleData.Fund.FundId, valueDate);
        q = q with { Subject = new ActorSubject(ActorType.Query, FundQueryActor.ActorName, GetClosingFundBalanceQuery.Verb, q.EntityId.Format()) };

        var context = Substitute.For<IQueryActorContext>();
        context.SetMessageInfo(Arg.Any<ActorThreadId>(), Arg.Any<string>(), Arg.Any<ActorMessageInfo>()).Returns(true);

        await actor.InvokeReceiveAsync(context, q);

        await context.Received(1)
            .ReplyAsync(Arg.Is<ActorThreadId>(id => id == q.Subject.ThreadId), Arg.Is<string>(v => v == GetClosingFundBalanceQuery.Verb), Arg.Is<ServiceResult<FundBalanceReadModel>>(r => r.Success && r.Value.Value == 123.45M));
    }

    [Fact]
    public async Task ReceiveAsync_GetFundDrawdownBalancesQuery_RepliesWithResult()
    {
        var logger = Substitute.For<ILogger<FundQueryActor>>();
        var fundDbContext = Substitute.For<IFundDbContext>();
        fundDbContext.GetFundStartingBalanceAsync(Arg.Any<int>(), Arg.Any<DateOnly>())
            .Returns(Task.FromResult(1000.00M));
        fundDbContext.GetFundEndingBalanceAsync(Arg.Any<int>(), Arg.Any<DateOnly>())
            .Returns(Task.FromResult(2000.00M));
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.FundDb
            .Returns(fundDbContext);
        var actor = _fixture.CreateActor(dbFactory, logger);
     
        var q = new GetFundDrawdownBalancesQuery(SampleData.Fund.FundId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)), DateOnly.FromDateTime(DateTime.UtcNow));
        q = q with { Subject = new ActorSubject(ActorType.Query, FundQueryActor.ActorName, GetFundDrawdownBalancesQuery.Verb, q.EntityId.Format()) };

        var context = Substitute.For<IQueryActorContext>();
        context.SetMessageInfo(Arg.Any<ActorThreadId>(), Arg.Any<string>(), Arg.Any<ActorMessageInfo>()).Returns(true);

        await actor.InvokeReceiveAsync(context, q);
        await context.Received(1)
            .ReplyAsync(Arg.Is<ActorThreadId>(id => id == q.Subject.ThreadId), Arg.Is<string>(v => v == GetFundDrawdownBalancesQuery.Verb), 
                Arg.Is<ServiceResult<FundDrawdownBalancesReadModel>>(r => r.Success && r.Value.StartBalance == 1000.00M && r.Value.EndBalance == 2000.00M));
    }

    [Fact]
    public async Task ReceiveAsync_GetFundIdFromOrderIdQuery_RepliesWithResult()
    {
        var logger = Substitute.For<ILogger<FundQueryActor>>();
        var fundDbContext = Substitute.For<IFundDbContext>();
        fundDbContext.GetFundIdFromOrderIdAsync(Arg.Any<int>())
            .Returns(Task.FromResult(456));
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.FundDb
            .Returns(fundDbContext);
        var actor = _fixture.CreateActor(dbFactory, logger);

        var q = new GetFundIdFromOrderIdQuery(456);
        q = q with { Subject = new ActorSubject(ActorType.Query, FundQueryActor.ActorName, GetFundIdFromOrderIdQuery.Verb, q.EntityId.Format()) };

        var context = Substitute.For<IQueryActorContext>();
        context.SetMessageInfo(Arg.Any<ActorThreadId>(), Arg.Any<string>(), Arg.Any<ActorMessageInfo>()).Returns(true);

        await actor.InvokeReceiveAsync(context, q);

        await context.Received(1)
            .ReplyAsync(Arg.Is<ActorThreadId>(id => id == q.Subject.ThreadId), Arg.Is<string>(v => v == GetFundIdFromOrderIdQuery.Verb), 
                Arg.Is<ServiceResult<ScalarReadModel<int>>>(r => r.Success && r.Value.Value == 456));
    }

    [Fact]
    public async Task ReceiveAsync_GetFundOrdersQuery_RepliesWithResults()
    {
        var logger = Substitute.For<ILogger<FundQueryActor>>();
        ICollection<FundOrderReadModel> fundOrders = new List<FundOrderReadModel> { SampleData.FundOrder };
        var fundDbContext = Substitute.For<IFundDbContext>();
        fundDbContext.GetFundOrdersAsync()
            .Returns(Task.FromResult(fundOrders));
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.FundDb
            .Returns(fundDbContext);
        var actor = _fixture.CreateActor(dbFactory, logger);

        var q = new GetFundOrdersQuery();
        q = q with { Subject = new ActorSubject(ActorType.Query, FundQueryActor.ActorName, GetFundOrdersQuery.Verb, q.EntityId.Format()) };

        var context = Substitute.For<IQueryActorContext>();
        context.SetMessageInfo(Arg.Any<ActorThreadId>(), Arg.Any<string>(), Arg.Any<ActorMessageInfo>()).Returns(true);

        await actor.InvokeReceiveAsync(context, q);

        await context.Received(1)
            .ReplyAsync(Arg.Is<ActorThreadId>(id => id == q.Subject.ThreadId), Arg.Is<string>(v => v == GetFundOrdersQuery.Verb), 
                Arg.Is<ServiceResult<FundOrderReadModel[]>>(r => r.Success && r.Value.Length == 1 && r.Value[0].OrderId == SampleData.FundOrder.OrderId));
    }

    [Fact]
    public async Task ReceiveAsync_GetFundOrderTradesQuery_RepliesWithResults()
    {
        var logger = Substitute.For<ILogger<FundQueryActor>>();
        ICollection<FundOrderTradeReadModel> trades = new List<FundOrderTradeReadModel> { SampleData.FundOrderTrade };
        var fundDbContext = Substitute.For<IFundDbContext>();
        fundDbContext.GetFundOrderTradesAsync()
            .Returns(Task.FromResult(trades));
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.FundDb
            .Returns(fundDbContext);
        var actor = _fixture.CreateActor(dbFactory, logger);

        var q = new GetFundOrderTradesQuery();
        q = q with { Subject = new ActorSubject(ActorType.Query, FundQueryActor.ActorName, GetFundOrderTradesQuery.Verb, q.EntityId.Format()) };

        var context = Substitute.For<IQueryActorContext>();
        context.SetMessageInfo(Arg.Any<ActorThreadId>(), Arg.Any<string>(), Arg.Any<ActorMessageInfo>()).Returns(true);

        await actor.InvokeReceiveAsync(context, q);

        await context.Received(1)
            .ReplyAsync(Arg.Is<ActorThreadId>(id => id == q.Subject.ThreadId), Arg.Is<string>(v => v == GetFundOrderTradesQuery.Verb),
                Arg.Is<ServiceResult<FundOrderTradeReadModel[]>>(r => r.Success && r.Value.Length == 1 && r.Value[0].TradeId == SampleData.FundOrderTrade.TradeId));
    }

    [Fact]
    public async Task ReceiveAsync_GetFundPnlReportQuery_RepliesWithResult()
    {
        var logger = Substitute.For<ILogger<FundQueryActor>>();
        var fundDbContext = Substitute.For<IFundDbContext>();
        ICollection<FundOrderAmountReadModel> fundLossOrders = new List<FundOrderAmountReadModel> { new FundOrderAmountReadModel { FundId = SampleData.Fund.FundId, OrderId = 1, ValueDate = DateOnly.FromDateTime(DateTime.UtcNow), Amount = -100m } };
        ICollection<FundOrderAmountReadModel> fundProfitOrders = new List<FundOrderAmountReadModel> { new FundOrderAmountReadModel { FundId = SampleData.Fund.FundId, OrderId = 2, ValueDate = DateOnly.FromDateTime(DateTime.UtcNow), Amount = 150m } };
        ICollection<FundDailyBalanceReadModel> fundBalances = new List<FundDailyBalanceReadModel> { new FundDailyBalanceReadModel(SampleData.Fund.FundId, DateOnly.FromDateTime(DateTime.UtcNow), 1000m) };
        fundDbContext.GetFundLossOrdersAsync(Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>())
            .Returns(Task.FromResult(fundLossOrders));
        fundDbContext.GetFundProfitOrdersAsync(Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>())
            .Returns(Task.FromResult(fundProfitOrders));
        fundDbContext.GetFundStartingBalanceAsync(Arg.Any<int>(), Arg.Any<DateOnly>())
            .Returns(Task.FromResult(1000m));
        fundDbContext.GetFundEndingBalanceAsync(Arg.Any<int>(), Arg.Any<DateOnly>())
            .Returns(Task.FromResult(100m));
        fundDbContext.GetFundTradeCommissionAsync(Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>())
            .Returns(Task.FromResult(10m));
        fundDbContext.GetFundDailyBalancesAsync(Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>())
            .Returns(Task.FromResult(fundBalances));
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.FundDb
            .Returns(fundDbContext);
        var actor = _fixture.CreateActor(dbFactory, logger);

        var q = new GetFundPnlReportQuery(SampleData.Fund.FundId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)), DateOnly.FromDateTime(DateTime.UtcNow));
        q = q with { Subject = new ActorSubject(ActorType.Query, FundQueryActor.ActorName, GetFundPnlReportQuery.Verb, q.EntityId.Format()) };

        var context = Substitute.For<IQueryActorContext>();
        context.SetMessageInfo(Arg.Any<ActorThreadId>(), Arg.Any<string>(), Arg.Any<ActorMessageInfo>()).Returns(true);

        await actor.InvokeReceiveAsync(context, q);

        await context
            .Received(1).ReplyAsync(Arg.Is<ActorThreadId>(id => id == q.Subject.ThreadId), Arg.Is<string>(v => v == GetFundPnlReportQuery.Verb), 
                Arg.Is<ServiceResult<FundPnlReportReadModel>>(r => r.Success && r.Value != null));
    }

    [Fact]
    public async Task ReceiveAsync_GetFundWinLossRatioQuery_RepliesWithResult()
    {
        var logger = Substitute.For<ILogger<FundQueryActor>>();
        var fundDbContext = Substitute.For<IFundDbContext>();
        ICollection<FundOrderAmountReadModel> fundLossOrders = new List<FundOrderAmountReadModel> { new FundOrderAmountReadModel { FundId = SampleData.Fund.FundId, OrderId = 1, ValueDate = DateOnly.FromDateTime(DateTime.UtcNow), Amount = -100m } };
        ICollection<FundOrderAmountReadModel> fundProfitOrders = new List<FundOrderAmountReadModel> { new FundOrderAmountReadModel { FundId = SampleData.Fund.FundId, OrderId = 2, ValueDate = DateOnly.FromDateTime(DateTime.UtcNow), Amount = 150m } };
        ICollection<FundDailyBalanceReadModel> fundBalances = new List<FundDailyBalanceReadModel> { new FundDailyBalanceReadModel(SampleData.Fund.FundId, DateOnly.FromDateTime(DateTime.UtcNow), 1000m) };
        fundDbContext.GetFundLossOrdersAsync(Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>())
            .Returns(Task.FromResult(fundLossOrders));
        fundDbContext.GetFundProfitOrdersAsync(Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>())
            .Returns(Task.FromResult(fundProfitOrders));
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.FundDb
            .Returns(fundDbContext);
        var actor = _fixture.CreateActor(dbFactory, logger);

        var q = new GetFundWinLossRatioQuery(SampleData.Fund.FundId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)), DateOnly.FromDateTime(DateTime.UtcNow));
        q = q with { Subject = new ActorSubject(ActorType.Query, FundQueryActor.ActorName, GetFundWinLossRatioQuery.Verb, q.EntityId.Format()) };

        var context = Substitute.For<IQueryActorContext>();
        context.SetMessageInfo(Arg.Any<ActorThreadId>(), Arg.Any<string>(), Arg.Any<ActorMessageInfo>()).Returns(true);

        await actor.InvokeReceiveAsync(context, q);

        await context
            .Received(1).ReplyAsync(Arg.Is<ActorThreadId>(id => id == q.Subject.ThreadId), Arg.Is<string>(v => v == GetFundWinLossRatioQuery.Verb), 
                Arg.Is<ServiceResult<FundWinLossRatioReadModel>>(r => r.Success && r.Value != null));
    }

    [Fact]
    public async Task ReceiveAsync_GetOpeningFundBalanceQuery_RepliesWithResult()
    {
        var logger = Substitute.For<ILogger<FundQueryActor>>();
        var fundDbContext = Substitute.For<IFundDbContext>();
        fundDbContext.GetOpeningFundBalanceAsync(Arg.Any<int>(), Arg.Any<DateOnly>())
            .Returns(Task.FromResult(77.7m));
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.FundDb
            .Returns(fundDbContext);
        var actor = _fixture.CreateActor(dbFactory, logger);

        var q = new GetOpeningFundBalanceQuery(SampleData.Fund.FundId, DateOnly.FromDateTime(DateTime.UtcNow));
        q = q with { Subject = new ActorSubject(ActorType.Query, FundQueryActor.ActorName, GetOpeningFundBalanceQuery.Verb, q.EntityId.Format()) };

        var context = Substitute.For<IQueryActorContext>();
        context.SetMessageInfo(Arg.Any<ActorThreadId>(), Arg.Any<string>(), Arg.Any<ActorMessageInfo>()).Returns(true);

        await actor.InvokeReceiveAsync(context, q);

        await context.Received(1)
            .ReplyAsync(Arg.Is<ActorThreadId>(id => id == q.Subject.ThreadId), Arg.Is<string>(v => v == GetOpeningFundBalanceQuery.Verb), 
                Arg.Is<ServiceResult<FundBalanceReadModel>>(r => r.Success && r.Value.Value == 77.7m));
    }

    [Fact]
    public async Task ReceiveAsync_GetFundMaxProfitGeneratedQuery_RepliesWithResult()
    {
        var logger = Substitute.For<ILogger<FundQueryActor>>();
        ICollection<FundOrderAmountReadModel> fundLossOrders = new List<FundOrderAmountReadModel> { new FundOrderAmountReadModel { FundId = SampleData.Fund.FundId, OrderId = 1, ValueDate = DateOnly.FromDateTime(DateTime.UtcNow), Amount = -100m } };
        ICollection<FundOrderAmountReadModel> fundProfitOrders = new List<FundOrderAmountReadModel> { new FundOrderAmountReadModel { FundId = SampleData.Fund.FundId, OrderId = 2, ValueDate = DateOnly.FromDateTime(DateTime.UtcNow), Amount = 150m } };
        var fundDbContext = Substitute.For<IFundDbContext>();
        fundDbContext.GetFundBalanceAsync(Arg.Any<int>())
            .Returns(Task.FromResult(1000m));
        fundDbContext.GetFundLossOrdersAsync(Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>())
           .Returns(Task.FromResult(fundLossOrders));
        fundDbContext.GetFundProfitOrdersAsync(Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>())
            .Returns(Task.FromResult(fundProfitOrders));
        fundDbContext.GetFundStartingBalanceAsync(Arg.Any<int>(), Arg.Any<DateOnly>())
             .Returns(Task.FromResult(1000m));
        fundDbContext.GetFundEndingBalanceAsync(Arg.Any<int>(), Arg.Any<DateOnly>())
            .Returns(Task.FromResult(100m));
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.FundDb
            .Returns(fundDbContext);
        var actor = _fixture.CreateActor(dbFactory, logger);

        var q = new GetFundMaxProfitGeneratedQuery(SampleData.Fund.FundId, DateOnly.FromDateTime(DateTime.UtcNow));
        var subject = new ActorSubject(ActorType.Query, FundQueryActor.ActorName, GetFundMaxProfitGeneratedQuery.Verb, q.EntityId.Format());
        // Subject is init-only on a class; set via reflection in tests
        var subjectProp = q.GetType().GetProperty("Subject");
        subjectProp?.SetValue(q, subject);

        var context = Substitute.For<IQueryActorContext>();
        context.SetMessageInfo(Arg.Any<ActorThreadId>(), Arg.Any<string>(), Arg.Any<ActorMessageInfo>()).Returns(true);

        await actor.InvokeReceiveAsync(context, q);

        await context.Received(1)
            .ReplyAsync(Arg.Is<ActorThreadId>(id => id == q.Subject.ThreadId), Arg.Is<string>(v => v == GetFundMaxProfitGeneratedQuery.Verb), 
                Arg.Is<ServiceResult<FundMaxProfitGeneratedReadModel>>(r => r.Success && r.Value != null && r.Value.FundId == q.FundId));
    }

    #endregion

    #region ReceiveAsync Tests

    [Fact]
    public async Task ReceiveAsync_GetFundBalanceQuery_ExecutesHandler_AndRepliesWithResult()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FundQueryActor>>();
        var fundDbContext = Substitute.For<IFundDbContext>();
        fundDbContext.GetFundBalanceAsync(Arg.Any<int>())
            .Returns(Task.FromResult(123.45m));
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.FundDb
            .Returns(fundDbContext);
        var actor = _fixture.CreateActor(dbFactory, logger);

        var query = new GetFundBalanceQuery(SampleData.Fund.FundId);
        query = query with { Subject = new ActorSubject(ActorType.Query, FundQueryActor.ActorName, GetFundBalanceQuery.Verb, query.EntityId.Format()) };
        var context = Substitute.For<IQueryActorContext>();
        context.SetMessageInfo(Arg.Any<ActorThreadId>(), Arg.Any<string>(), Arg.Any<ActorMessageInfo>()).Returns(true);

        // Act
        await actor.InvokeReceiveAsync(context, query);

        // Assert
        await context.Received(1).ReplyAsync(
            Arg.Is<ActorThreadId>(id => id == query.Subject.ThreadId),
            Arg.Is<string>(v => v == GetFundBalanceQuery.Verb),
            Arg.Is<ServiceResult<FundBalanceReadModel>>(r => r.Success && r.Value != null && r.Value.Value == 123.45m)
        );
    }

    [Fact]
    public async Task ReceiveAsync_GetFundsQuery_WithEmptyResults_RepliesWithEmptyArray()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FundQueryActor>>();
        ICollection<FundReadModel> funds = [];
        var fundDbContext = Substitute.For<IFundDbContext>();
        fundDbContext.GetFundsAsync()
            .Returns(Task.FromResult(funds));
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.FundDb
            .Returns(fundDbContext);
        var actor = _fixture.CreateActor(dbFactory, logger);

        var query = new GetFundsQuery();
        query = query with { Subject = new ActorSubject(ActorType.Query, FundQueryActor.ActorName, GetFundsQuery.Verb, query.EntityId.Format()) };

        var context = Substitute.For<IQueryActorContext>();
        context.SetMessageInfo(Arg.Any<ActorThreadId>(), Arg.Any<string>(), Arg.Any<ActorMessageInfo>()).Returns(true);

        // Act
        await actor.InvokeReceiveAsync(context, query);

        // Assert
        await context.Received(1).ReplyAsync(
            Arg.Is<ActorThreadId>(id => id == query.Subject.ThreadId),
            Arg.Is<string>(v => v == GetFundsQuery.Verb),
            Arg.Is<ServiceResult<FundReadModel[]>>(r => r.Success && r.Value != null && r.Value.Length == 0)
        );
    }

    [Fact]
    public async Task ReceiveAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FundQueryActor>>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        var actor = _fixture.CreateActor(dbFactory, logger);

        var query = new GetFundBalanceQuery(SampleData.Fund.FundId);
        query = query with { Subject = new ActorSubject(ActorType.Query, FundQueryActor.ActorName, GetFundBalanceQuery.Verb, query.EntityId.Format()) };

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(null!, query);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReceiveAsync_ThrowsInvalidOperationException_WhenQueryUnsupported()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FundQueryActor>>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        var actor = _fixture.CreateActor(dbFactory, logger);

        var dummy = Substitute.For<IQuery>();
        // substitute a fake type name by creating a local anonymous type implementing IQuery is not possible, instead use a mock and ensure its runtime type name is unsupported

        var context = Substitute.For<IQueryActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(context, dummy);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    #endregion

}