using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ScyllaDb.TradeDb;
using TomasAI.IFM.Domain.Trade.Queries;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.Queries;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Queries;

public class TradeQueryActorTests : IClassFixture<TradeFixture>
{
    readonly TradeFixture _fixture;

    public TradeQueryActorTests(TradeFixture fixture)
    {
        _fixture = fixture;
    }

    public class TestableTradeQueryActor(IDbContextFactory dbFactory, ILogger<TradeQueryActor> logger)
        : TradeQueryActor(dbFactory, logger)
    {
        public IQuery InvokeParseMessage(IQueryActorContext context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public async ValueTask InvokeReceiveAsync(IQueryActorContext context, IQuery query)
            => await ReceiveAsync(context, query);


        public async ValueTask InvokeOnExceptionAsync(IQueryActorContext context, ActorThreadId threadId, IQuery query, string verb, Exception ex)
            => await OnExceptionAsync(context, threadId, query, verb, ex);
    }

    static readonly OptionTradeEntityId _entityId = SampleData.EntityId1; // orderId=100, tradeId=1

    // --- helpers ----------------------------------------------------------------

    NatsMsg<byte[]> MakeMsg<TQuery>(TQuery query) where TQuery : class, IQuery
    {
        var data = _fixture.DataSerializer.Serialize(query);
        return new NatsMsg<byte[]>(query.Subject.ToString(), string.Empty, 0, default!, data, default!, NatsMsgFlags.None);
    }

    #region ParseMessage � Happy Path

    [Fact]
    public void ParseMessage_ShouldParseGetTradeHistoryQuery_Successfully()
    {
        // Arrange
        var actor = _fixture.CreateTradeQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var query = new GetTradeHistoryQuery(orderId: 100)
        {
            Subject = new ActorSubject(ActorType.Query, TradeQueryActor.ActorName, GetTradeHistoryQuery.Verb, _entityId.Format())
        };
        var message = MakeMsg(query);

        // Act
        var result = actor.InvokeParseMessage(context, message);

        // Assert
        result.Should().NotBeNull().And.BeOfType<GetTradeHistoryQuery>();
        ((GetTradeHistoryQuery)result).OrderId.Should().Be(100);
        context.Received(1).SetMessageInfo(
            Arg.Any<ActorThreadId>(),
            Arg.Is(GetTradeHistoryQuery.Verb),
            Arg.Any<ActorMessageInfo>());
    }

    [Fact]
    public void ParseMessage_ShouldParseGetTradeLimitQuery_Successfully()
    {
        // Arrange
        var actor = _fixture.CreateTradeQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var query = new GetTradeLimitQuery(tradeId: 1)
        {
            Subject = new ActorSubject(ActorType.Query, TradeQueryActor.ActorName, GetTradeLimitQuery.Verb, _entityId.Format())
        };
        var message = MakeMsg(query);

        // Act
        var result = actor.InvokeParseMessage(context, message);

        // Assert
        result.Should().NotBeNull().And.BeOfType<GetTradeLimitQuery>();
        ((GetTradeLimitQuery)result).TradeId.Should().Be(1);
        context.Received(1).SetMessageInfo(
            Arg.Any<ActorThreadId>(),
            Arg.Is(GetTradeLimitQuery.Verb),
            Arg.Any<ActorMessageInfo>());
    }

    [Fact]
    public void ParseMessage_ShouldParseGetTradePositionQuery_Successfully()
    {
        // Arrange
        var actor = _fixture.CreateTradeQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var valueDate = new DateOnly(2025, 1, 15);
        var query = new GetTradePositionQuery(100, 1, TradeType.ShortIronCondor, valueDate, 45, TradeStatus.Open)
        {
            Subject = new ActorSubject(ActorType.Query, TradeQueryActor.ActorName, GetTradePositionQuery.Verb, _entityId.Format())
        };
        var message = MakeMsg(query);

        // Act
        var result = actor.InvokeParseMessage(context, message);

        // Assert
        result.Should().NotBeNull().And.BeOfType<GetTradePositionQuery>();
        var parsed = (GetTradePositionQuery)result;
        parsed.OrderId.Should().Be(100);
        parsed.TradeId.Should().Be(1);
        parsed.TradeType.Should().Be(TradeType.ShortIronCondor);
        parsed.ValueDate.Should().Be(valueDate);
        context.Received(1).SetMessageInfo(
            Arg.Any<ActorThreadId>(),
            Arg.Is(GetTradePositionQuery.Verb),
            Arg.Any<ActorMessageInfo>());
    }

    [Fact]
    public void ParseMessage_ShouldParseGetTradeQuantityQuery_Successfully()
    {
        // Arrange
        var actor = _fixture.CreateTradeQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var query = new GetTradeQuantityQuery(tradeId: 1)
        {
            Subject = new ActorSubject(ActorType.Query, TradeQueryActor.ActorName, GetTradeQuantityQuery.Verb, _entityId.Format())
        };
        var message = MakeMsg(query);

        // Act
        var result = actor.InvokeParseMessage(context, message);

        // Assert
        result.Should().NotBeNull().And.BeOfType<GetTradeQuantityQuery>();
        ((GetTradeQuantityQuery)result).TradeId.Should().Be(1);
        context.Received(1).SetMessageInfo(
            Arg.Any<ActorThreadId>(),
            Arg.Is(GetTradeQuantityQuery.Verb),
            Arg.Any<ActorMessageInfo>());
    }

    [Fact]
    public void ParseMessage_ShouldParseGetTradeTypeLimitQuery_Successfully()
    {
        // Arrange
        var actor = _fixture.CreateTradeQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var query = new GetTradeTypeLimitQuery(tradeId: 1, TradeType.ShortIronCondor)
        {
            Subject = new ActorSubject(ActorType.Query, TradeQueryActor.ActorName, GetTradeTypeLimitQuery.Verb, _entityId.Format())
        };
        var message = MakeMsg(query);

        // Act
        var result = actor.InvokeParseMessage(context, message);

        // Assert
        result.Should().NotBeNull().And.BeOfType<GetTradeTypeLimitQuery>();
        var parsed = (GetTradeTypeLimitQuery)result;
        parsed.TradeId.Should().Be(1);
        parsed.TradeType.Should().Be(TradeType.ShortIronCondor);
        context.Received(1).SetMessageInfo(
            Arg.Any<ActorThreadId>(),
            Arg.Is(GetTradeTypeLimitQuery.Verb),
            Arg.Any<ActorMessageInfo>());
    }

    [Fact]
    public void ParseMessage_ShouldExtractThreadIdFromSubject_Correctly()
    {
        // Arrange
        var actor = _fixture.CreateTradeQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var entityId = SampleData.EntityId2; // orderId=200, tradeId=2
        var query = new GetTradeHistoryQuery(orderId: 200)
        {
            Subject = new ActorSubject(ActorType.Query, TradeQueryActor.ActorName, GetTradeHistoryQuery.Verb, entityId.Format())
        };
        var message = MakeMsg(query);

        // Act
        actor.InvokeParseMessage(context, message);

        // Assert
        context.Received(1).SetMessageInfo(
            Arg.Is<ActorThreadId>(tid => tid == query.Subject.ThreadId),
            Arg.Is(GetTradeHistoryQuery.Verb),
            Arg.Any<ActorMessageInfo>());
    }

    #endregion

    #region ParseMessage � Edge Cases

    [Fact]
    public void ParseMessage_ShouldThrowArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var actor = _fixture.CreateTradeQueryActor();
        var query = new GetTradeHistoryQuery(orderId: 100)
        {
            Subject = new ActorSubject(ActorType.Query, TradeQueryActor.ActorName, GetTradeHistoryQuery.Verb, _entityId.Format())
        };
        var message = MakeMsg(query);

        // Act & Assert
        var act = () => actor.InvokeParseMessage(null!, message);
        act.Should().Throw<ArgumentNullException>().WithParameterName("context");
    }

    [Fact]
    public void ParseMessage_ShouldThrowInvalidOperationException_WhenActorTypeIsNotQuery()
    {
        // Arrange
        var actor = _fixture.CreateTradeQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var invalidSubject = new ActorSubject(ActorType.Command, TradeQueryActor.ActorName, GetTradeHistoryQuery.Verb, _entityId.Format());
        var message = new NatsMsg<byte[]>(invalidSubject.ToString(), string.Empty, 0, default!, Array.Empty<byte>(), default!, NatsMsgFlags.None);

        // Act & Assert
        var act = () => actor.InvokeParseMessage(context, message);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {TradeQueryActor.ActorName} query from message: *");
    }

    [Fact]
    public void ParseMessage_ShouldThrowInvalidOperationException_WhenActorNameIsIncorrect()
    {
        // Arrange
        var actor = _fixture.CreateTradeQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var invalidSubject = new ActorSubject(ActorType.Query, "WrongActor", GetTradeHistoryQuery.Verb, _entityId.Format());
        var message = new NatsMsg<byte[]>(invalidSubject.ToString(), string.Empty, 0, default!, Array.Empty<byte>(), default!, NatsMsgFlags.None);

        // Act & Assert
        var act = () => actor.InvokeParseMessage(context, message);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {TradeQueryActor.ActorName} query from message: *");
    }

    [Fact]
    public void ParseMessage_ShouldThrowInvalidOperationException_WhenVerbIsNotInParseMap()
    {
        // Arrange
        var actor = _fixture.CreateTradeQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var invalidSubject = new ActorSubject(ActorType.Query, TradeQueryActor.ActorName, "UnknownVerb", _entityId.Format());
        var message = new NatsMsg<byte[]>(invalidSubject.ToString(), string.Empty, 0, default!, Array.Empty<byte>(), default!, NatsMsgFlags.None);

        // Act & Assert
        var act = () => actor.InvokeParseMessage(context, message);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {TradeQueryActor.ActorName} query from message: *");
    }

    [Fact]
    public void ParseMessage_ShouldThrowArgumentNullException_WhenPayloadIsEmpty()
    {
        // Arrange
        var actor = _fixture.CreateTradeQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var validSubject = new ActorSubject(ActorType.Query, TradeQueryActor.ActorName, GetTradeHistoryQuery.Verb, _entityId.Format());
        var message = new NatsMsg<byte[]>(validSubject.ToString(), string.Empty, 0, default!, Array.Empty<byte>(), default!, NatsMsgFlags.None);

        // Act & Assert
        var act = () => actor.InvokeParseMessage(context, message);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ParseMessage_ShouldThrowException_WhenPayloadIsCorrupted()
    {
        // Arrange
        var actor = _fixture.CreateTradeQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var corruptedPayload = new byte[] { 0x00, 0x01, 0x02, 0xFF, 0xFE };
        var validSubject = new ActorSubject(ActorType.Query, TradeQueryActor.ActorName, GetTradeHistoryQuery.Verb, _entityId.Format());
        var message = new NatsMsg<byte[]>(validSubject.ToString(), string.Empty, 0, default!, corruptedPayload, default!, NatsMsgFlags.None);

        // Act & Assert
        var act = () => actor.InvokeParseMessage(context, message);
        act.Should().Throw<Exception>();
    }

    #endregion

    #region ReceiveAsync � Happy Path

    [Fact]
    public async Task ReceiveAsync_ShouldProcessGetTradeHistoryQuery_Successfully()
    {
        // Arrange
        var dbFactory = Substitute.For<IDbContextFactory>();
        var tradeDb = Substitute.For<ITradeDbContext>();
        dbFactory.TradeDb.Returns(tradeDb);

        var actor = _fixture.CreateTradeQueryActor(dbFactory);
        var context = Substitute.For<IQueryActorContext>();

        var query = new GetTradeHistoryQuery(orderId: 100)
        {
            Subject = new ActorSubject(ActorType.Query, TradeQueryActor.ActorName, GetTradeHistoryQuery.Verb, _entityId.Format())
        };

        var msgInfo = new ActorMessageInfo(
            new NatsMsg<byte[]>(query.Subject.ToString(), string.Empty, 0, default!, Array.Empty<byte>(), default!, NatsMsgFlags.None),
            query);
        context.GetMessageInfo(query.Subject.ThreadId, GetTradeHistoryQuery.Verb).Returns(msgInfo);

        // Act
        await actor.InvokeReceiveAsync(context, query);

        // Assert � TradeDb repository was accessed during the query
        _ = dbFactory.Received().TradeDb;
    }

    [Fact]
    public async Task ReceiveAsync_ShouldProcessGetTradeLimitQuery_Successfully()
    {
        // Arrange
        var dbFactory = Substitute.For<IDbContextFactory>();
        var tradeDb = Substitute.For<ITradeDbContext>();
        dbFactory.TradeDb.Returns(tradeDb);

        var actor = _fixture.CreateTradeQueryActor(dbFactory);
        var context = Substitute.For<IQueryActorContext>();

        var query = new GetTradeLimitQuery(tradeId: 1)
        {
            Subject = new ActorSubject(ActorType.Query, TradeQueryActor.ActorName, GetTradeLimitQuery.Verb, _entityId.Format())
        };
        var msgInfo = new ActorMessageInfo(
            new NatsMsg<byte[]>(query.Subject.ToString(), string.Empty, 0, default!, Array.Empty<byte>(), default!, NatsMsgFlags.None),
            query);
        context.GetMessageInfo(query.Subject.ThreadId, GetTradeLimitQuery.Verb).Returns(msgInfo);

        // Act
        await actor.InvokeReceiveAsync(context, query);

        // Assert
        _ = dbFactory.Received().TradeDb;
    }

    [Fact]
    public async Task ReceiveAsync_ShouldProcessGetTradePositionQuery_Successfully()
    {
        // Arrange
        var dbFactory = Substitute.For<IDbContextFactory>();
        var tradeDb = Substitute.For<ITradeDbContext>();
        dbFactory.TradeDb.Returns(tradeDb);

        var actor = _fixture.CreateTradeQueryActor(dbFactory);
        var context = Substitute.For<IQueryActorContext>();

        var query = new GetTradePositionQuery(100, 1, TradeType.ShortIronCondor, new DateOnly(2025, 1, 15), 45, TradeStatus.Open)
        {
            Subject = new ActorSubject(ActorType.Query, TradeQueryActor.ActorName, GetTradePositionQuery.Verb, _entityId.Format())
        };
        var msgInfo = new ActorMessageInfo(
            new NatsMsg<byte[]>(query.Subject.ToString(), string.Empty, 0, default!, Array.Empty<byte>(), default!, NatsMsgFlags.None),
            query);
        context.GetMessageInfo(query.Subject.ThreadId, GetTradePositionQuery.Verb).Returns(msgInfo);

        // Act
        await actor.InvokeReceiveAsync(context, query);

        // Assert
        _ = dbFactory.Received().TradeDb;
    }

    [Fact]
    public async Task ReceiveAsync_ShouldProcessGetTradeQuantityQuery_Successfully()
    {
        // Arrange
        var dbFactory = Substitute.For<IDbContextFactory>();
        var tradeDb = Substitute.For<ITradeDbContext>();
        dbFactory.TradeDb.Returns(tradeDb);

        var actor = _fixture.CreateTradeQueryActor(dbFactory);
        var context = Substitute.For<IQueryActorContext>();

        var query = new GetTradeQuantityQuery(tradeId: 1)
        {
            Subject = new ActorSubject(ActorType.Query, TradeQueryActor.ActorName, GetTradeQuantityQuery.Verb, _entityId.Format())
        };
        var msgInfo = new ActorMessageInfo(
            new NatsMsg<byte[]>(query.Subject.ToString(), string.Empty, 0, default!, Array.Empty<byte>(), default!, NatsMsgFlags.None),
            query);
        context.GetMessageInfo(query.Subject.ThreadId, GetTradeQuantityQuery.Verb).Returns(msgInfo);

        // Act
        await actor.InvokeReceiveAsync(context, query);

        // Assert
        _ = dbFactory.Received().TradeDb;
    }

    [Fact]
    public async Task ReceiveAsync_ShouldProcessGetTradeTypeLimitQuery_Successfully()
    {
        // Arrange
        var dbFactory = Substitute.For<IDbContextFactory>();
        var tradeDb = Substitute.For<ITradeDbContext>();
        dbFactory.TradeDb.Returns(tradeDb);

        var actor = _fixture.CreateTradeQueryActor(dbFactory);
        var context = Substitute.For<IQueryActorContext>();

        var query = new GetTradeTypeLimitQuery(tradeId: 1, TradeType.ShortIronCondor)
        {
            Subject = new ActorSubject(ActorType.Query, TradeQueryActor.ActorName, GetTradeTypeLimitQuery.Verb, _entityId.Format())
        };
        var msgInfo = new ActorMessageInfo(
            new NatsMsg<byte[]>(query.Subject.ToString(), string.Empty, 0, default!, Array.Empty<byte>(), default!, NatsMsgFlags.None),
            query);
        context.GetMessageInfo(query.Subject.ThreadId, GetTradeTypeLimitQuery.Verb).Returns(msgInfo);

        // Act
        await actor.InvokeReceiveAsync(context, query);

        // Assert
        _ = dbFactory.Received().TradeDb;
    }

    #endregion

    #region ReceiveAsync � Edge Cases

    [Fact]
    public async Task ReceiveAsync_ShouldThrowArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var actor = _fixture.CreateTradeQueryActor();
        var query = new GetTradeHistoryQuery(orderId: 100)
        {
            Subject = new ActorSubject(ActorType.Query, TradeQueryActor.ActorName, GetTradeHistoryQuery.Verb, _entityId.Format())
        };

        // Act & Assert
        var act = async () => await actor.InvokeReceiveAsync(null!, query);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("context");
    }

    [Fact]
    public async Task ReceiveAsync_ShouldThrowArgumentNullException_WhenQueryIsNull()
    {
        // Arrange
        var actor = _fixture.CreateTradeQueryActor();
        var context = Substitute.For<IQueryActorContext>();

        // Act & Assert
        var act = async () => await actor.InvokeReceiveAsync(context, null!);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("query");
    }

    [Fact]
    public async Task ReceiveAsync_ShouldThrowInvalidOperationException_WhenQueryTypeIsNotSupported()
    {
        // Arrange
        var actor = _fixture.CreateTradeQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var unsupportedQuery = Substitute.For<IQuery>();
        unsupportedQuery.Subject.Returns(new ActorSubject(ActorType.Query, TradeQueryActor.ActorName, "UnsupportedVerb", _entityId.Format()));

        // Act & Assert
        var act = async () => await actor.InvokeReceiveAsync(context, unsupportedQuery);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to process {TradeQueryActor.ActorName} query: *");
    }

    #endregion

    #region OnExceptionAsync � Happy Path

    [Fact]
    public async Task OnExceptionAsync_ShouldHandleGetTradeHistoryQuery_Exception()
    {
        // Arrange
        var actor = _fixture.CreateTradeQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var threadId = new ActorThreadId(ActorType.Query, TradeQueryActor.ActorName, _entityId.Format());
        var query = new GetTradeHistoryQuery(orderId: 100)
        {
            Subject = new ActorSubject(ActorType.Query, TradeQueryActor.ActorName, GetTradeHistoryQuery.Verb, _entityId.Format())
        };
        var exception = new Exception("Trade history failed");

        // Act
        await actor.InvokeOnExceptionAsync(context, threadId, query, GetTradeHistoryQuery.Verb, exception);

        // Assert
        await context.Received(1).ReplyAsync(
            threadId,
            GetTradeHistoryQuery.Verb,
            Arg.Is<ServiceResult<TradeHistoryReadModel[]>>(r =>
                !r.Success &&
                r.ErrorCode == GetTradeHistoryQuery.ErrorId &&
                r.ErrorMessage == "Trade history failed"));
    }

    [Fact]
    public async Task OnExceptionAsync_ShouldHandleGetTradeLimitQuery_Exception()
    {
        // Arrange
        var actor = _fixture.CreateTradeQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var threadId = new ActorThreadId(ActorType.Query, TradeQueryActor.ActorName, _entityId.Format());
        var query = new GetTradeLimitQuery(tradeId: 1)
        {
            Subject = new ActorSubject(ActorType.Query, TradeQueryActor.ActorName, GetTradeLimitQuery.Verb, _entityId.Format())
        };
        var exception = new Exception("Trade limit failed");

        // Act
        await actor.InvokeOnExceptionAsync(context, threadId, query, GetTradeLimitQuery.Verb, exception);

        // Assert
        await context.Received(1).ReplyAsync(
            threadId,
            GetTradeLimitQuery.Verb,
            Arg.Is<ServiceResult<TradeLimitReadModel>>(r =>
                !r.Success &&
                r.ErrorCode == GetTradeLimitQuery.ErrorId &&
                r.ErrorMessage == "Trade limit failed"));
    }

    [Fact]
    public async Task OnExceptionAsync_ShouldHandleGetTradePositionQuery_Exception()
    {
        // Arrange
        var actor = _fixture.CreateTradeQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var threadId = new ActorThreadId(ActorType.Query, TradeQueryActor.ActorName, _entityId.Format());
        var query = new GetTradePositionQuery(100, 1, TradeType.ShortIronCondor, new DateOnly(2025, 1, 15), 45, TradeStatus.Open)
        {
            Subject = new ActorSubject(ActorType.Query, TradeQueryActor.ActorName, GetTradePositionQuery.Verb, _entityId.Format())
        };
        var exception = new Exception("Trade position failed");

        // Act
        await actor.InvokeOnExceptionAsync(context, threadId, query, GetTradePositionQuery.Verb, exception);

        // Assert
        await context.Received(1).ReplyAsync(
            threadId,
            GetTradePositionQuery.Verb,
            Arg.Is<ServiceResult<TradePositionReadModel>>(r =>
                !r.Success &&
                r.ErrorCode == GetTradePositionQuery.ErrorId &&
                r.ErrorMessage == "Trade position failed"));
    }

    [Fact]
    public async Task OnExceptionAsync_ShouldHandleGetTradeQuantityQuery_Exception()
    {
        // Arrange
        var actor = _fixture.CreateTradeQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var threadId = new ActorThreadId(ActorType.Query, TradeQueryActor.ActorName, _entityId.Format());
        var query = new GetTradeQuantityQuery(tradeId: 1)
        {
            Subject = new ActorSubject(ActorType.Query, TradeQueryActor.ActorName, GetTradeQuantityQuery.Verb, _entityId.Format())
        };
        var exception = new Exception("Trade quantity failed");

        // Act
        await actor.InvokeOnExceptionAsync(context, threadId, query, GetTradeQuantityQuery.Verb, exception);

        // Assert
        await context.Received(1).ReplyAsync(
            threadId,
            GetTradeQuantityQuery.Verb,
            Arg.Is<ServiceResult<ScalarReadModel<int>>>(r =>
                !r.Success &&
                r.ErrorCode == GetTradeQuantityQuery.ErrorId &&
                r.ErrorMessage == "Trade quantity failed"));
    }

    [Fact]
    public async Task OnExceptionAsync_ShouldHandleGetTradeTypeLimitQuery_Exception()
    {
        // Arrange
        var actor = _fixture.CreateTradeQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var threadId = new ActorThreadId(ActorType.Query, TradeQueryActor.ActorName, _entityId.Format());
        var query = new GetTradeTypeLimitQuery(tradeId: 1, TradeType.ShortIronCondor)
        {
            Subject = new ActorSubject(ActorType.Query, TradeQueryActor.ActorName, GetTradeTypeLimitQuery.Verb, _entityId.Format())
        };
        var exception = new Exception("Trade type limit failed");

        // Act
        await actor.InvokeOnExceptionAsync(context, threadId, query, GetTradeTypeLimitQuery.Verb, exception);

        // Assert
        await context.Received(1).ReplyAsync(
            threadId,
            GetTradeTypeLimitQuery.Verb,
            Arg.Is<ServiceResult<TradeTypeLimitReadModel>>(r =>
                !r.Success &&
                r.ErrorCode == GetTradeTypeLimitQuery.ErrorId &&
                r.ErrorMessage == "Trade type limit failed"));
    }

    [Fact]
    public async Task OnExceptionAsync_ShouldHandleUnknownQuery_WithServiceFailed()
    {
        // Arrange
        var actor = _fixture.CreateTradeQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var threadId = new ActorThreadId(ActorType.Query, TradeQueryActor.ActorName, _entityId.Format());
        var unknownQuery = Substitute.For<IQuery>();
        unknownQuery.ErrorCode.Returns(9999);
        var exception = new Exception("Unknown query");

        // Act
        await actor.InvokeOnExceptionAsync(context, threadId, unknownQuery, "UnknownVerb", exception);

        // Assert
        await context.Received(1).ReplyAsync(
            threadId,
            "UnknownVerb",
            Arg.Any<ServiceFailed<ActorEntityId>>());
    }

    #endregion

    #region OnExceptionAsync � Edge Cases

    [Fact]
    public async Task OnExceptionAsync_ShouldThrowArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var actor = _fixture.CreateTradeQueryActor();
        var threadId = new ActorThreadId(ActorType.Query, TradeQueryActor.ActorName, _entityId.Format());
        var query = new GetTradeHistoryQuery(orderId: 100)
        {
            Subject = new ActorSubject(ActorType.Query, TradeQueryActor.ActorName, GetTradeHistoryQuery.Verb, _entityId.Format())
        };
        var exception = new Exception("Error");

        // Act & Assert
        var act = async () => await actor.InvokeOnExceptionAsync(null!, threadId, query, GetTradeHistoryQuery.Verb, exception);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("context");
    }

    [Fact]
    public async Task OnExceptionAsync_ShouldNotThrow_WhenReplyAsyncFails()
    {
        // Arrange
        var actor = _fixture.CreateTradeQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var threadId = new ActorThreadId(ActorType.Query, TradeQueryActor.ActorName, _entityId.Format());
        var query = new GetTradeHistoryQuery(orderId: 100)
        {
            Subject = new ActorSubject(ActorType.Query, TradeQueryActor.ActorName, GetTradeHistoryQuery.Verb, _entityId.Format())
        };
        context.ReplyAsync(Arg.Any<ActorThreadId>(), Arg.Any<string>(), Arg.Any<ServiceResult<TradeHistoryReadModel[]>>())
            .Returns(x => throw new InvalidOperationException("reply failure"));
        var exception = new Exception("Original error");

        // Act � inner exception is swallowed and logged; no outer throw
        var act = async () => await actor.InvokeOnExceptionAsync(context, threadId, query, GetTradeHistoryQuery.Verb, exception);
        await act.Should().NotThrowAsync();
    }

    #endregion
}

