using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ScyllaDb.FundDb;
using TomasAI.IFM.Domain.Fund.Shared.Queries;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Fund.Transaction.Query;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Fund.UnitTests.Transaction;

/// <summary>
/// Contains unit tests for <see cref="FundTransactionQueryActor"/>, covering ParseMessage, ReceiveAsync and
/// OnExceptionAsync behaviors, including happy paths and edge cases.
/// </summary>
public class FundTransactionQueryActorTests : IClassFixture<FundTestFixture>
{
    readonly FundTestFixture _fixture;

    public FundTransactionQueryActorTests(FundTestFixture fixture)
    {
        _fixture = fixture;
    }

    // Test helper to expose protected ParseMessage, ReceiveAsync and OnExceptionAsync for unit testing.
    public class TestableFundTransactionQueryActor : FundTransactionQueryActor
    {
        public TestableFundTransactionQueryActor(IDbContextFactory dbFactory, ILogger<FundTransactionQueryActor> logger)
            : base(dbFactory, logger)
        {
        }

        public IQuery InvokeParseMessage(IQueryActorContext context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public async ValueTask InvokeReceiveAsync(IQueryActorContext context, IActorState state, IQuery query)
            => await ReceiveAsync(context, state, query);

        public async ValueTask InvokeOnExceptionAsync(IQueryActorContext context, ActorThreadId threadId, IQuery query, string verb, Exception ex)
            => await OnExceptionAsync(context, threadId, query, verb, ex);
    }

    static (IDbContextFactory DbFactory, IFundDbContext FundDb) CreateDbFactory()
    {
        var fundDb = Substitute.For<IFundDbContext>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.FundDb.Returns(fundDb);
        return (dbFactory, fundDb);
    }

    static IQueryActorContext CreateContext()
    {
        var context = Substitute.For<IQueryActorContext>();
        context.SetMessageInfo(Arg.Any<ActorThreadId>(), Arg.Any<string>(), Arg.Any<ActorMessageInfo>()).Returns(true);
        return context;
    }

    static GetFundTransactionsQuery CreateQuery(string verb = GetFundTransactionsQuery.Verb)
    {
        var q = new GetFundTransactionsQuery(SampleData.FundTransaction.FundId, SampleData.FundTransaction.ValueDate, SampleData.FundTransaction.ValueDate.AddDays(1));
        return q with { Subject = new ActorSubject(ActorType.Query, FundTransactionQueryActor.ActorName, verb, q.EntityId.Format()) };
    }

    #region ParseMessage

    [Fact]
    public void ParseMessage_GivenValidGetFundTransactionsMessage_WhenParsed_ThenReturnsQueryAndSetsMessageInfo()
    {
        // Arrange
        var (dbFactory, _) = CreateDbFactory();
        var logger = Substitute.For<ILogger<FundTransactionQueryActor>>();
        var actor = _fixture.CreateActor(dbFactory, logger);
        var query = CreateQuery();
        var payload = ActorExtensions.DataSerializer.Serialize(query);
        var natsMsg = new NatsMsg<byte[]>(query.Subject.ToString(), string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);
        var context = CreateContext();

        // Act
        var result = actor.InvokeParseMessage(context, natsMsg);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<GetFundTransactionsQuery>();
        context.Received(1).SetMessageInfo(
            Arg.Is<ActorThreadId>(id => id == query.Subject.ThreadId),
            Arg.Is<string>(v => v == GetFundTransactionsQuery.Verb),
            Arg.Is<ActorMessageInfo>(info => info.ActorMessage.Subject == query.Subject.ToString()));
    }

    [Fact]
    public void ParseMessage_GivenNullContext_WhenParsed_ThenThrowsArgumentNullException()
    {
        // Arrange
        var (dbFactory, _) = CreateDbFactory();
        var actor = _fixture.CreateActor(dbFactory, Substitute.For<ILogger<FundTransactionQueryActor>>());
        var query = CreateQuery();
        var payload = ActorExtensions.DataSerializer.Serialize(query);
        var natsMsg = new NatsMsg<byte[]>(query.Subject.ToString(), string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        // Act
        Action act = () => actor.InvokeParseMessage(null!, natsMsg);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ParseMessage_GivenMessageForDifferentActor_WhenParsed_ThenThrowsInvalidOperationException()
    {
        // Arrange
        var (dbFactory, _) = CreateDbFactory();
        var actor = _fixture.CreateActor(dbFactory, Substitute.For<ILogger<FundTransactionQueryActor>>());
        var subject = new ActorSubject(ActorType.Query, "SomeOtherActor", GetFundTransactionsQuery.Verb, "1");
        var natsMsg = new NatsMsg<byte[]>(subject.ToString(), string.Empty, 0, default!, Array.Empty<byte>(), default!, NatsMsgFlags.None);
        var context = CreateContext();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FundTransactionQueryActor.ActorName} query from message:*");
    }

    [Fact]
    public void ParseMessage_GivenMessageWithUnsupportedVerb_WhenParsed_ThenThrowsInvalidOperationException()
    {
        // Arrange
        var (dbFactory, _) = CreateDbFactory();
        var actor = _fixture.CreateActor(dbFactory, Substitute.For<ILogger<FundTransactionQueryActor>>());
        var subject = new ActorSubject(ActorType.Query, FundTransactionQueryActor.ActorName, "UnknownVerb", "1");
        var natsMsg = new NatsMsg<byte[]>(subject.ToString(), string.Empty, 0, default!, Array.Empty<byte>(), default!, NatsMsgFlags.None);
        var context = CreateContext();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region ReceiveAsync

    [Fact]
    public async Task ReceiveAsync_GivenExistingTransactionsInRange_WhenExecuted_ThenRepliesWithTransactions()
    {
        // Arrange
        var (dbFactory, fundDb) = CreateDbFactory();
        var startDate = SampleData.FundTransaction.ValueDate;
        var endDate = startDate.AddDays(1);
        ICollection<FundTransactionReadModel> transactions = [SampleData.FundTransaction];
        fundDb.GetFundTransactionsAsync(SampleData.FundTransaction.FundId, startDate, endDate).Returns(Task.FromResult(transactions));
        var actor = _fixture.CreateActor(dbFactory, Substitute.For<ILogger<FundTransactionQueryActor>>());
        var context = CreateContext();
        var query = CreateQuery();

        // Act
        await actor.InvokeReceiveAsync(context, default!, query);

        // Assert
        await context.Received(1).ReplyAsync(
            Arg.Is<ActorThreadId>(id => id == query.Subject.ThreadId),
            Arg.Is<string>(v => v == GetFundTransactionsQuery.Verb),
            Arg.Is<ServiceResult<ICollection<FundTransactionReadModel>>>(r =>
                r.Success && r.Value.Count == 1 && r.Value.First().TransactionId == SampleData.FundTransaction.TransactionId));
    }

    [Fact]
    public async Task ReceiveAsync_GivenNoTransactionsInRange_WhenExecuted_ThenRepliesWithEmptyResult()
    {
        // Arrange
        var (dbFactory, fundDb) = CreateDbFactory();
        var startDate = SampleData.FundTransaction.ValueDate;
        var endDate = startDate.AddDays(1);
        ICollection<FundTransactionReadModel> transactions = [];
        fundDb.GetFundTransactionsAsync(SampleData.FundTransaction.FundId, startDate, endDate).Returns(Task.FromResult(transactions));
        var actor = _fixture.CreateActor(dbFactory, Substitute.For<ILogger<FundTransactionQueryActor>>());
        var context = CreateContext();
        var query = CreateQuery();

        // Act
        await actor.InvokeReceiveAsync(context, default!, query);

        // Assert
        await context.Received(1).ReplyAsync(
            Arg.Is<ActorThreadId>(id => id == query.Subject.ThreadId),
            Arg.Is<string>(v => v == GetFundTransactionsQuery.Verb),
            Arg.Is<ServiceResult<ICollection<FundTransactionReadModel>>>(r => r.Success && r.Value.Count == 0));
    }

    [Fact]
    public async Task ReceiveAsync_GivenUnsupportedQuery_WhenExecuted_ThenThrowsInvalidOperationException()
    {
        // Arrange
        var (dbFactory, _) = CreateDbFactory();
        var actor = _fixture.CreateActor(dbFactory, Substitute.For<ILogger<FundTransactionQueryActor>>());
        var context = CreateContext();
        var unsupportedQuery = Substitute.For<IQuery>();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(context, default!, unsupportedQuery);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ReceiveAsync_GivenNullContext_WhenExecuted_ThenThrowsArgumentNullException()
    {
        // Arrange
        var (dbFactory, _) = CreateDbFactory();
        var actor = _fixture.CreateActor(dbFactory, Substitute.For<ILogger<FundTransactionQueryActor>>());
        var query = CreateQuery();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(null!, default!, query);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReceiveAsync_GivenNullQuery_WhenExecuted_ThenThrowsArgumentNullException()
    {
        // Arrange
        var (dbFactory, _) = CreateDbFactory();
        var actor = _fixture.CreateActor(dbFactory, Substitute.For<ILogger<FundTransactionQueryActor>>());
        var context = CreateContext();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(context, default!, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region OnExceptionAsync

    [Fact]
    public async Task OnExceptionAsync_GivenGetFundTransactionsQuery_WhenExceptionOccurs_ThenRepliesWithTypedFailure()
    {
        // Arrange
        var (dbFactory, _) = CreateDbFactory();
        var actor = _fixture.CreateActor(dbFactory, Substitute.For<ILogger<FundTransactionQueryActor>>());
        var context = CreateContext();
        context.ReplyAsync(Arg.Any<ActorThreadId>(), Arg.Any<string>(), Arg.Any<ServiceResult<FundTransactionReadModel[]>>())
            .Returns(ValueTask.CompletedTask);
        var query = CreateQuery();
        var ex = new Exception("boom");

        // Act
        await actor.InvokeOnExceptionAsync(context, query.Subject.ThreadId, query, GetFundTransactionsQuery.Verb, ex);

        // Assert
        await context.Received(1).ReplyAsync(
            Arg.Is<ActorThreadId>(id => id == query.Subject.ThreadId),
            Arg.Is<string>(v => v == GetFundTransactionsQuery.Verb),
            Arg.Is<ServiceResult<FundTransactionReadModel[]>>(r => !r.Success && r.ErrorCode == query.ErrorCode && r.ErrorMessage == ex.Message));
    }

    [Fact]
    public async Task OnExceptionAsync_GivenUnsupportedQuery_WhenExceptionOccurs_ThenRepliesWithGenericFailure()
    {
        // Arrange
        var (dbFactory, _) = CreateDbFactory();
        var actor = _fixture.CreateActor(dbFactory, Substitute.For<ILogger<FundTransactionQueryActor>>());
        var context = CreateContext();
        context.ReplyAsync(Arg.Any<ActorThreadId>(), Arg.Any<string>(), Arg.Any<ServiceFailed<ActorEntityId>>())
            .Returns(ValueTask.CompletedTask);
        var unsupportedQuery = Substitute.For<IQuery>();
        var threadId = new ActorThreadId(ActorType.Query, FundTransactionQueryActor.ActorName, "1");
        var ex = new Exception("boom");

        // Act
        await actor.InvokeOnExceptionAsync(context, threadId, unsupportedQuery, "UnknownVerb", ex);

        // Assert
        await context.Received(1).ReplyAsync(
            Arg.Is<ActorThreadId>(id => id == threadId),
            Arg.Is<string>(v => v == "UnknownVerb"),
            Arg.Is<ServiceFailed<ActorEntityId>>(r => !r.Success && r.ErrorCode == 9999 && r.ErrorMessage == ex.Message));
    }

    [Fact]
    public async Task OnExceptionAsync_GivenReplyAsyncThrows_WhenExceptionOccurs_ThenSwallowsInnerExceptionAndLogs()
    {
        // Arrange
        var (dbFactory, _) = CreateDbFactory();
        var logger = Substitute.For<ILogger<FundTransactionQueryActor>>();
        var actor = _fixture.CreateActor(dbFactory, logger);
        var context = CreateContext();
        context.ReplyAsync(Arg.Any<ActorThreadId>(), Arg.Any<string>(), Arg.Any<ServiceResult<FundTransactionReadModel[]>>())
            .Returns(ValueTask.FromException(new InvalidOperationException("inner failure")));
        var query = CreateQuery();
        var ex = new Exception("boom");

        // Act
        Func<Task> act = async () => await actor.InvokeOnExceptionAsync(context, query.Subject.ThreadId, query, GetFundTransactionsQuery.Verb, ex);

        // Assert - inner exception is caught and logged, not propagated
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OnExceptionAsync_GivenNullContext_WhenExceptionOccurs_ThenThrowsArgumentNullException()
    {
        // Arrange - the IsArgumentNull.Check guards run before the try/catch, so a null context
        // is a programming error that must propagate rather than being silently logged.
        var (dbFactory, _) = CreateDbFactory();
        var actor = _fixture.CreateActor(dbFactory, Substitute.For<ILogger<FundTransactionQueryActor>>());
        var query = CreateQuery();
        var ex = new Exception("boom");

        // Act
        Func<Task> act = async () => await actor.InvokeOnExceptionAsync(null!, query.Subject.ThreadId, query, GetFundTransactionsQuery.Verb, ex);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion
}
