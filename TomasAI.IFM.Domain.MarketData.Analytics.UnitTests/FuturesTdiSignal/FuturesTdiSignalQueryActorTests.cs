using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Query;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Queries;
using TomasAI.IFM.Shared.MarketDataAnalytics.QueryParameters;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Query.Actor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesTdiSignal;

public class FuturesTdiSignalQueryActorTests : IClassFixture<MarketDataAnalyticsTestFixture>
{
    readonly MarketDataAnalyticsTestFixture _fixture;

    public FuturesTdiSignalQueryActorTests(MarketDataAnalyticsTestFixture fixture)
    {
        _fixture = fixture;
    }

    public class TestableFuturesTdiSignalQueryActor : FuturesTdiSignalQueryActor
    {
        public TestableFuturesTdiSignalQueryActor(IDbContextFactory dbFactory, ILogger<FuturesTdiSignalQueryActor> logger)
            : base(dbFactory, logger)
        {
        }

        public IQuery InvokeParseMessage(IQueryActorContext context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public async ValueTask InvokeReceiveAsync(IQueryActorContext context, IQuery query)
            => await ReceiveAsync(context, query);

        public async ValueTask InvokeOnExceptionAsync(IQueryActorContext context, ActorThreadId threadId, IQuery query, string verb, Exception ex)
            => await OnExceptionAsync(context, threadId, query, verb, ex);


    }

    #region ParseMessage Happy Path Tests

    [Fact]
    public void ParseMessage_ShouldParseGetFuturesTdiSignalQuery_Successfully()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesTdiSignalQueryActor>>();
        var actor = _fixture.CreateTdiQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new GetFuturesTdiSignalParameter(SampleData.ContractId, SampleData.ValueDate);
        var query = new GetFuturesTdiSignalQuery
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesTdiSignalQuery.Actor, GetFuturesTdiSignalQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ContractId = SampleData.ContractId,
            ValueDate = SampleData.ValueDate
        };
        var serializedData = _fixture.DataSerializer.Serialize(query);
        var message = new NatsMsg<byte[]>
        {
            Subject = query.Subject.ToString(),
            Data = serializedData
        };

        // Act
        var result = actor.InvokeParseMessage(context, message);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<GetFuturesTdiSignalQuery>();
        var parsedQuery = result as GetFuturesTdiSignalQuery;
        parsedQuery!.Subject.Should().BeEquivalentTo(query.Subject);
        context.Received(1).SetMessageInfo(
            Arg.Any<ActorThreadId>(),
            Arg.Is(GetFuturesTdiSignalQuery.Verb),
            Arg.Any<ActorMessageInfo>());
    }

    #endregion

    #region ParseMessage Edge Case Tests

    [Fact]
    public void ParseMessage_ShouldThrowArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesTdiSignalQueryActor>>();
        var actor = _fixture.CreateTdiQueryActor(logger);
        var entityId = new GetFuturesTdiSignalParameter(SampleData.ContractId, SampleData.ValueDate);
        var query = new GetFuturesTdiSignalQuery
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesTdiSignalQuery.Actor, GetFuturesTdiSignalQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ContractId = SampleData.ContractId,
            ValueDate = SampleData.ValueDate
        };
        var serializedData = _fixture.DataSerializer.Serialize(query);
        var message = new NatsMsg<byte[]>
        {
            Subject = query.Subject.ToString(),
            Data = serializedData
        };

        // Act & Assert
        var act = () => actor.InvokeParseMessage(null!, message);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("context");
    }

    [Fact]
    public void ParseMessage_ShouldThrowInvalidOperationException_WhenActorTypeIsNotQuery()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesTdiSignalQueryActor>>();
        var actor = _fixture.CreateTdiQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new GetFuturesTdiSignalParameter(SampleData.ContractId, SampleData.ValueDate);
        var invalidSubject = new ActorSubject(ActorType.Command, GetFuturesTdiSignalQuery.Actor, GetFuturesTdiSignalQuery.Verb, entityId.Format());
        var message = new NatsMsg<byte[]>
        {
            Subject = invalidSubject.ToString(),
            Data = Array.Empty<byte>()
        };

        // Act & Assert
        var act = () => actor.InvokeParseMessage(context, message);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Unable to resolve FuturesTdiSignalQuery query from message: *");
    }

    [Fact]
    public void ParseMessage_ShouldThrowInvalidOperationException_WhenActorNameIsIncorrect()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesTdiSignalQueryActor>>();
        var actor = _fixture.CreateTdiQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new GetFuturesTdiSignalParameter(SampleData.ContractId, SampleData.ValueDate);
        var invalidSubject = new ActorSubject(ActorType.Query, "WrongActor", GetFuturesTdiSignalQuery.Verb, entityId.Format());
        var message = new NatsMsg<byte[]>
        {
            Subject = invalidSubject.ToString(),
            Data = Array.Empty<byte>()
        };

        // Act & Assert
        var act = () => actor.InvokeParseMessage(context, message);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Unable to resolve FuturesTdiSignalQuery query from message: *");
    }

    [Fact]
    public void ParseMessage_ShouldThrowInvalidOperationException_WhenVerbIsNotInParseMap()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesTdiSignalQueryActor>>();
        var actor = _fixture.CreateTdiQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new GetFuturesTdiSignalParameter(SampleData.ContractId, SampleData.ValueDate);
        var invalidSubject = new ActorSubject(ActorType.Query, GetFuturesTdiSignalQuery.Actor, "UnknownVerb", entityId.Format());
        var message = new NatsMsg<byte[]>
        {
            Subject = invalidSubject.ToString(),
            Data = Array.Empty<byte>()
        };

        // Act & Assert
        var act = () => actor.InvokeParseMessage(context, message);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Unable to resolve FuturesTdiSignalQuery query from message: *");
    }

    [Fact]
    public void ParseMessage_ShouldThrowArgumentNullException_WhenDeserializedQueryIsNull()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesTdiSignalQueryActor>>();
        var actor = _fixture.CreateTdiQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new GetFuturesTdiSignalParameter(SampleData.ContractId, SampleData.ValueDate);
        var validSubject = new ActorSubject(ActorType.Query, GetFuturesTdiSignalQuery.Actor, GetFuturesTdiSignalQuery.Verb, entityId.Format());
        var message = new NatsMsg<byte[]>
        {
            Subject = validSubject.ToString(),
            Data = Array.Empty<byte>()
        };

        // Act & Assert
        var act = () => actor.InvokeParseMessage(context, message);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ParseMessage_ShouldHandleEmptyEntityId_InSubject()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesTdiSignalQueryActor>>();
        var actor = _fixture.CreateTdiQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var subjectWithEmptyEntityId = new ActorSubject(ActorType.Query, GetFuturesTdiSignalQuery.Actor, GetFuturesTdiSignalQuery.Verb, string.Empty);
        var query = new GetFuturesTdiSignalQuery
        {
            Subject = subjectWithEmptyEntityId,
            EntityId = new GetFuturesTdiSignalParameter(SampleData.ContractId, SampleData.ValueDate),
            ContractId = SampleData.ContractId,
            ValueDate = SampleData.ValueDate
        };
        var serializedData = _fixture.DataSerializer.Serialize(query);
        var message = new NatsMsg<byte[]>
        {
            Subject = subjectWithEmptyEntityId.ToString(),
            Data = serializedData
        };

        // Act
        var result = actor.InvokeParseMessage(context, message);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<GetFuturesTdiSignalQuery>();
    }

    [Fact]
    public void ParseMessage_ShouldExtractThreadIdFromSubject_Correctly()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesTdiSignalQueryActor>>();
        var actor = _fixture.CreateTdiQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new GetFuturesTdiSignalParameter(SampleData.ContractId, SampleData.ValueDate);
        var query = new GetFuturesTdiSignalQuery
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesTdiSignalQuery.Actor, GetFuturesTdiSignalQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ContractId = SampleData.ContractId,
            ValueDate = SampleData.ValueDate
        };
        var serializedData = _fixture.DataSerializer.Serialize(query);
        var message = new NatsMsg<byte[]>
        {
            Subject = query.Subject.ToString(),
            Data = serializedData
        };

        // Act
        var result = actor.InvokeParseMessage(context, message);

        // Assert
        result.Should().NotBeNull();
        context.Received(1).SetMessageInfo(
            Arg.Is<ActorThreadId>(tid => tid == query.Subject.ThreadId),
            Arg.Is(GetFuturesTdiSignalQuery.Verb),
            Arg.Any<ActorMessageInfo>());
    }

    [Fact]
    public void ParseMessage_ShouldSetMessageInfoCorrectly_WithAllComponents()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesTdiSignalQueryActor>>();
        var actor = _fixture.CreateTdiQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new GetFuturesTdiSignalParameter(SampleData.ContractId, SampleData.ValueDate);
        var query = new GetFuturesTdiSignalQuery
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesTdiSignalQuery.Actor, GetFuturesTdiSignalQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ContractId = SampleData.ContractId,
            ValueDate = SampleData.ValueDate
        };
        var serializedData = _fixture.DataSerializer.Serialize(query);
        var message = new NatsMsg<byte[]>
        {
            Subject = query.Subject.ToString(),
            Data = serializedData
        };

        // Act
        var result = actor.InvokeParseMessage(context, message);

        // Assert
        context.Received(1).SetMessageInfo(
            Arg.Any<ActorThreadId>(),
            Arg.Is<string>(v => v == GetFuturesTdiSignalQuery.Verb),
            Arg.Any<ActorMessageInfo>());
    }

    #endregion

    #region ReceiveAsync Happy Path Tests

    [Fact]
    public async Task ReceiveAsync_ShouldProcessGetFuturesTdiSignalQuery_Successfully()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesTdiSignalQueryActor>>();
        var context = Substitute.For<IQueryActorContext>();
        var db = Substitute.For<IMarketDataDbContext>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.MarketDataDb.Returns(db);
        var actor = _fixture.CreateTdiQueryActor(logger, dbFactory);
        var entityId = new GetFuturesTdiSignalParameter(SampleData.ContractId, SampleData.ValueDate);
        var threadId = new ActorThreadId(ActorType.Query, FuturesTdiSignalQueryActor.ActorName, entityId.Format());

        db.GetLastFuturesTdiSignalAsync(SampleData.ContractId, SampleData.ValueDate)
            .Returns((FuturesTdiSignalReadModel?)null);

        var query = new GetFuturesTdiSignalQuery
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesTdiSignalQuery.Actor, GetFuturesTdiSignalQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ContractId = SampleData.ContractId,
            ValueDate = SampleData.ValueDate
        };
        var natsMsg = new NatsMsg<byte[]> { Subject = query.Subject.ToString(), Data = _fixture.DataSerializer.Serialize(query) };
        context.GetMessageInfo(threadId, GetFuturesTdiSignalQuery.Verb).Returns(new ActorMessageInfo(natsMsg, query));

        // Act
        await actor.InvokeReceiveAsync(context, query);

        // Assert
        await db.Received(1).GetLastFuturesTdiSignalAsync(SampleData.ContractId, SampleData.ValueDate);
    }

    [Fact]
    public async Task ReceiveAsync_ShouldProcessGetFuturesTdiSignalQuery_WhenDataExists()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesTdiSignalQueryActor>>();
        var context = Substitute.For<IQueryActorContext>();
        var db = Substitute.For<IMarketDataDbContext>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.MarketDataDb.Returns(db);
        var actor = _fixture.CreateTdiQueryActor(logger, dbFactory);
        var entityId = new GetFuturesTdiSignalParameter(SampleData.ContractId, SampleData.ValueDate);
        var threadId = new ActorThreadId(ActorType.Query, FuturesTdiSignalQueryActor.ActorName, entityId.Format());

        var expectedViewModel = new FuturesTdiSignalReadModel(
            contractId: SampleData.ContractId,
            valueDate: SampleData.ValueDate,
            timePeriod: TradeTimePeriodType.Daily,
            timestamp: new TimeOnly(10, 0, 0),
            upTrendCount: 8,
            downTrendCount: 7,
            tdi: FuturesTrendDirectionType.UpTrending,
            tdiStrength: FuturesTrendDirectionStrengthType.Medium);

        db.GetLastFuturesTdiSignalAsync(SampleData.ContractId, SampleData.ValueDate)
            .Returns(expectedViewModel);

        var query = new GetFuturesTdiSignalQuery
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesTdiSignalQuery.Actor, GetFuturesTdiSignalQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ContractId = SampleData.ContractId,
            ValueDate = SampleData.ValueDate
        };
        var natsMsg = new NatsMsg<byte[]> { Subject = query.Subject.ToString(), Data = _fixture.DataSerializer.Serialize(query) };
        context.GetMessageInfo(threadId, GetFuturesTdiSignalQuery.Verb).Returns(new ActorMessageInfo(natsMsg, query));

        // Act
        await actor.InvokeReceiveAsync(context, query);

        // Assert
        await db.Received(1).GetLastFuturesTdiSignalAsync(SampleData.ContractId, SampleData.ValueDate);
    }

    #endregion

    #region ReceiveAsync Edge Case Tests

    [Fact]
    public async Task ReceiveAsync_ShouldThrowArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesTdiSignalQueryActor>>();
        var actor = _fixture.CreateTdiQueryActor(logger);
        var db = Substitute.For<IMarketDataDbContext>();
        var entityId = new GetFuturesTdiSignalParameter(SampleData.ContractId, SampleData.ValueDate);
        var query = new GetFuturesTdiSignalQuery
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesTdiSignalQuery.Actor, GetFuturesTdiSignalQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ContractId = SampleData.ContractId,
            ValueDate = SampleData.ValueDate
        };

        // Act & Assert
        var act = async () => await actor.InvokeReceiveAsync(null!, query);
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("context");
    }

    [Fact]
    public async Task ReceiveAsync_ShouldThrowArgumentNullException_WhenQueryIsNull()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesTdiSignalQueryActor>>();
        var actor = _fixture.CreateTdiQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var db = Substitute.For<IMarketDataDbContext>();
        var entityId = new GetFuturesTdiSignalParameter(SampleData.ContractId, SampleData.ValueDate);

        // Act & Assert
        var act = async () => await actor.InvokeReceiveAsync(context, null!);
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("query");
    }

    [Fact]
    public async Task ReceiveAsync_ShouldThrowInvalidOperationException_WhenQueryTypeIsNotSupported()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesTdiSignalQueryActor>>();
        var actor = _fixture.CreateTdiQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var db = Substitute.For<IMarketDataDbContext>();
        var entityId = new GetFuturesTdiSignalParameter(SampleData.ContractId, SampleData.ValueDate);

        var unsupportedQuery = Substitute.For<IQuery>();
        unsupportedQuery.Subject.Returns(new ActorSubject(ActorType.Query, FuturesTdiSignalQueryActor.ActorName, "UnsupportedVerb", entityId.Format()));

        // Act & Assert
        var act = async () => await actor.InvokeReceiveAsync(context, unsupportedQuery);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Unable to process FuturesTdiSignalQuery query: *");
    }

    #endregion

    #region OnExceptionAsync Happy Path Tests

    [Fact]
    public async Task OnExceptionAsync_ShouldHandleGetFuturesTdiSignalQuery_Exception()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesTdiSignalQueryActor>>();
        var actor = _fixture.CreateTdiQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new GetFuturesTdiSignalParameter(SampleData.ContractId, SampleData.ValueDate);
        var threadId = new ActorThreadId(ActorType.Query, FuturesTdiSignalQueryActor.ActorName, entityId.Format());
        var query = new GetFuturesTdiSignalQuery
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesTdiSignalQuery.Actor, GetFuturesTdiSignalQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ContractId = SampleData.ContractId,
            ValueDate = SampleData.ValueDate,
            ErrorCode = 500
        };
        var exception = new Exception("Database connection failed");

        // Act
        await actor.InvokeOnExceptionAsync(context, threadId, query, GetFuturesTdiSignalQuery.Verb, exception);

        // Assert
        await context.Received(1).ReplyAsync(
            threadId,
            GetFuturesTdiSignalQuery.Verb,
            Arg.Is<ServiceResult<FuturesTdiSignalReadModel?>>(r =>
                !r.Success &&
                r.ErrorCode == 500 &&
                r.ErrorMessage == "Database connection failed"));
    }

    #endregion

    #region OnExceptionAsync Edge Case Tests

    [Fact]
    public async Task OnExceptionAsync_ShouldThrowArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesTdiSignalQueryActor>>();
        var actor = _fixture.CreateTdiQueryActor(logger);
        var entityId = new GetFuturesTdiSignalParameter(SampleData.ContractId, SampleData.ValueDate);
        var threadId = new ActorThreadId(ActorType.Query, FuturesTdiSignalQueryActor.ActorName, entityId.Format());
        var query = new GetFuturesTdiSignalQuery
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesTdiSignalQuery.Actor, GetFuturesTdiSignalQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ContractId = SampleData.ContractId,
            ValueDate = SampleData.ValueDate
        };
        var exception = new Exception("Test exception");

        // Act & Assert
        var act = async () => await actor.InvokeOnExceptionAsync(null!, threadId, query, GetFuturesTdiSignalQuery.Verb, exception);
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("context");
    }

    [Fact]
    public async Task OnExceptionAsync_ShouldThrowArgumentNullException_WhenThreadIdIsNull()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesTdiSignalQueryActor>>();
        var actor = _fixture.CreateTdiQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new GetFuturesTdiSignalParameter(SampleData.ContractId, SampleData.ValueDate);
        var query = new GetFuturesTdiSignalQuery
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesTdiSignalQuery.Actor, GetFuturesTdiSignalQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ContractId = SampleData.ContractId,
            ValueDate = SampleData.ValueDate
        };
        var exception = new Exception("Test exception");

        // Act & Assert
        var act = async () => await actor.InvokeOnExceptionAsync(context, default, query, GetFuturesTdiSignalQuery.Verb, exception);
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("threadId");
    }

    [Fact]
    public async Task OnExceptionAsync_ShouldThrowArgumentNullException_WhenQueryIsNull()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesTdiSignalQueryActor>>();
        var actor = _fixture.CreateTdiQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new GetFuturesTdiSignalParameter(SampleData.ContractId, SampleData.ValueDate);
        var threadId = new ActorThreadId(ActorType.Query, FuturesTdiSignalQueryActor.ActorName, entityId.Format());

        // Act & Assert
        var act = async () => await actor.InvokeOnExceptionAsync(context, threadId, null!, GetFuturesTdiSignalQuery.Verb, null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnExceptionAsync_ShouldThrowArgumentNullException_WhenVerbIsNull()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesTdiSignalQueryActor>>();
        var actor = _fixture.CreateTdiQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new GetFuturesTdiSignalParameter(SampleData.ContractId, SampleData.ValueDate);
        var threadId = new ActorThreadId(ActorType.Query, FuturesTdiSignalQueryActor.ActorName, entityId.Format());
        var query = new GetFuturesTdiSignalQuery
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesTdiSignalQuery.Actor, GetFuturesTdiSignalQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ContractId = SampleData.ContractId,
            ValueDate = SampleData.ValueDate
        };
        var exception = new Exception("Test exception");

        // Act & Assert
        var act = async () => await actor.InvokeOnExceptionAsync(context, threadId, query, null!, exception);
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("verb");
    }

    [Fact]
    public async Task OnExceptionAsync_ShouldHandleUnknownQueryType_WithDefaultResponse()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesTdiSignalQueryActor>>();
        var actor = _fixture.CreateTdiQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new GetFuturesTdiSignalParameter(SampleData.ContractId, SampleData.ValueDate);
        var threadId = new ActorThreadId(ActorType.Query, FuturesTdiSignalQueryActor.ActorName, entityId.Format());

        var unknownQuery = Substitute.For<IQuery>();
        unknownQuery.Subject.Returns(new ActorSubject(ActorType.Query, FuturesTdiSignalQueryActor.ActorName, "UnknownVerb", entityId.Format()));
        unknownQuery.ErrorCode.Returns(9999);

        var exception = new Exception("Unknown query type");

        // Act
        await actor.InvokeOnExceptionAsync(context, threadId, unknownQuery, "UnknownVerb", exception);

        // Assert
        await context.Received(1).ReplyAsync(
            threadId,
            "UnknownVerb",
            Arg.Is<ServiceFailed<ActorEntityId>>(r =>
                !r.Success &&
                r.ErrorCode == 9999 &&
                r.ErrorMessage == "Unknown query type"));
    }

    [Fact]
    public async Task OnExceptionAsync_ShouldHandleNestedExceptions()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesTdiSignalQueryActor>>();
        var actor = _fixture.CreateTdiQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new GetFuturesTdiSignalParameter(SampleData.ContractId, SampleData.ValueDate);
        var threadId = new ActorThreadId(ActorType.Query, FuturesTdiSignalQueryActor.ActorName, entityId.Format());
        var query = new GetFuturesTdiSignalQuery
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesTdiSignalQuery.Actor, GetFuturesTdiSignalQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ContractId = SampleData.ContractId,
            ValueDate = SampleData.ValueDate,
            ErrorCode = 500
        };
        var innerException = new InvalidOperationException("Inner error");
        var exception = new Exception("Outer error", innerException);

        // Act
        await actor.InvokeOnExceptionAsync(context, threadId, query, GetFuturesTdiSignalQuery.Verb, exception);

        // Assert
        await context.Received(1).ReplyAsync(
            threadId,
            GetFuturesTdiSignalQuery.Verb,
            Arg.Is<ServiceResult<FuturesTdiSignalReadModel?>>(r =>
                !r.Success &&
                r.ErrorCode == 500 &&
                r.ErrorMessage == "Outer error"));
    }

    [Fact]
    public async Task OnExceptionAsync_ShouldHandleExceptionWhenReplyAsyncThrows()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesTdiSignalQueryActor>>();
        var actor = _fixture.CreateTdiQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new GetFuturesTdiSignalParameter(SampleData.ContractId, SampleData.ValueDate);
        var threadId = new ActorThreadId(ActorType.Query, FuturesTdiSignalQueryActor.ActorName, entityId.Format());
        var query = new GetFuturesTdiSignalQuery
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesTdiSignalQuery.Actor, GetFuturesTdiSignalQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ContractId = SampleData.ContractId,
            ValueDate = SampleData.ValueDate,
            ErrorCode = 500
        };
        var exception = new Exception("Original error");

        context.ReplyAsync(threadId, GetFuturesTdiSignalQuery.Verb, Arg.Any<ServiceResult<FuturesTdiSignalReadModel?>>())
            .Returns<ValueTask>(_ => throw new InvalidOperationException("Reply failed"));

        // Act - should not throw, should be caught and logged
        var act = async () => await actor.InvokeOnExceptionAsync(context, threadId, query, GetFuturesTdiSignalQuery.Verb, exception);
        await act.Should().NotThrowAsync();

        // Assert - verify ReplyAsync was called (and it threw, which was caught)
        await context.Received(1).ReplyAsync(
            threadId,
            GetFuturesTdiSignalQuery.Verb,
            Arg.Any<ServiceResult<FuturesTdiSignalReadModel?>>());
    }

    #endregion

}
