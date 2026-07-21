using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Query;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed.Queries;
using TomasAI.IFM.Shared.MarketDataFeed.QueryParameters;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Query.Actor;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.FuturesTickData;

public class FuturesTickDataQueryActorTests : IClassFixture<MarketDataFeedTestFixture>
{
    readonly MarketDataFeedTestFixture _fixture;

    public FuturesTickDataQueryActorTests(MarketDataFeedTestFixture fixture)
    {
        _fixture = fixture;
    }

    public class TestableFuturesTickDataQueryActor : FuturesTickDataQueryActor
    {
        public TestableFuturesTickDataQueryActor(IDbContextFactory dbFactory, ILogger<FuturesTickDataQueryActor> logger)
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
    public void ParseMessage_ShouldParseGetLastFuturesTickDataQuery_Successfully()
    {
        // Arrange
        var actor = _fixture.CreateFuturesTickDataQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new GetLastFuturesTickDataParameter(SampleData.EsTickData.ContractId, SampleData.ValueDate);
        var query = new GetLastFuturesTickDataQuery(entityId.ContractId, entityId.ValueDate)
        {
            Subject = new ActorSubject(ActorType.Query, FuturesTickDataQueryActor.ActorName, GetLastFuturesTickDataQuery.Verb, entityId.Format()),
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
        result.Should().BeOfType<GetLastFuturesTickDataQuery>();
        var parsedQuery = result as GetLastFuturesTickDataQuery;
        parsedQuery!.Subject.Should().BeEquivalentTo(query.Subject);
        parsedQuery.ContractId.Should().Be(entityId.ContractId);
        parsedQuery.ValueDate.Should().Be(entityId.ValueDate);
        context.Received(1).SetMessageInfo(
            Arg.Any<ActorThreadId>(),
            Arg.Is(GetLastFuturesTickDataQuery.Verb),
            Arg.Any<ActorMessageInfo>());
    }

    [Fact]
    public void ParseMessage_ShouldParseGetLastFuturesTickDataByTickDateQuery_Successfully()
    {
        // Arrange
        var actor = _fixture.CreateFuturesTickDataQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var tickDate = new DateTime(2024, 6, 15, 14, 30, 0, DateTimeKind.Utc);
        var entityId = new GetLastFuturesTickDataByTickDateParameter(SampleData.EsTickData.ContractId, tickDate);
        var query = new GetLastFuturesTickDataByTickDateQuery(entityId.ContractId, entityId.TickDate)
        {
            Subject = new ActorSubject(ActorType.Query, FuturesTickDataQueryActor.ActorName, GetLastFuturesTickDataByTickDateQuery.Verb, entityId.Format()),
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
        result.Should().BeOfType<GetLastFuturesTickDataByTickDateQuery>();
        var parsedQuery = result as GetLastFuturesTickDataByTickDateQuery;
        parsedQuery!.Subject.Should().BeEquivalentTo(query.Subject);
        parsedQuery.ContractId.Should().Be(entityId.ContractId);
        parsedQuery.TickDate.Should().Be(entityId.TickDate);
        context.Received(1).SetMessageInfo(
            Arg.Any<ActorThreadId>(),
            Arg.Is(GetLastFuturesTickDataByTickDateQuery.Verb),
            Arg.Any<ActorMessageInfo>());
    }

    #endregion

    #region ParseMessage Edge Case Tests

    [Fact]
    public void ParseMessage_ShouldThrowArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var actor = _fixture.CreateFuturesTickDataQueryActor();
        var entityId = new GetLastFuturesTickDataParameter(SampleData.EsTickData.ContractId, SampleData.ValueDate);
        var query = new GetLastFuturesTickDataQuery(entityId.ContractId, entityId.ValueDate)
        {
            Subject = new ActorSubject(ActorType.Query, FuturesTickDataQueryActor.ActorName, GetLastFuturesTickDataQuery.Verb, entityId.Format()),
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
        var actor = _fixture.CreateFuturesTickDataQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new GetLastFuturesTickDataParameter(SampleData.EsTickData.ContractId, SampleData.ValueDate);
        var invalidSubject = new ActorSubject(ActorType.Command, FuturesTickDataQueryActor.ActorName, GetLastFuturesTickDataQuery.Verb, entityId.Format());
        var message = new NatsMsg<byte[]>
        {
            Subject = invalidSubject.ToString(),
            Data = Array.Empty<byte>()
        };

        // Act & Assert
        var act = () => actor.InvokeParseMessage(context, message);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Unable to resolve FuturesTickDataQuery query from message: *");
    }

    [Fact]
    public void ParseMessage_ShouldThrowInvalidOperationException_WhenActorNameIsIncorrect()
    {
        // Arrange
        var actor = _fixture.CreateFuturesTickDataQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new GetLastFuturesTickDataParameter(SampleData.EsTickData.ContractId, SampleData.ValueDate);
        var invalidSubject = new ActorSubject(ActorType.Query, "WrongActor", GetLastFuturesTickDataQuery.Verb, entityId.Format());
        var message = new NatsMsg<byte[]>
        {
            Subject = invalidSubject.ToString(),
            Data = Array.Empty<byte>()
        };

        // Act & Assert
        var act = () => actor.InvokeParseMessage(context, message);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Unable to resolve FuturesTickDataQuery query from message: *");
    }

    [Fact]
    public void ParseMessage_ShouldThrowInvalidOperationException_WhenVerbIsNotInParseMap()
    {
        // Arrange
        var actor = _fixture.CreateFuturesTickDataQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new GetLastFuturesTickDataParameter(SampleData.EsTickData.ContractId, SampleData.ValueDate);
        var invalidSubject = new ActorSubject(ActorType.Query, FuturesTickDataQueryActor.ActorName, "UnknownVerb", entityId.Format());
        var message = new NatsMsg<byte[]>
        {
            Subject = invalidSubject.ToString(),
            Data = Array.Empty<byte>()
        };

        // Act & Assert
        var act = () => actor.InvokeParseMessage(context, message);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Unable to resolve FuturesTickDataQuery query from message: *");
    }

    [Fact]
    public void ParseMessage_ShouldThrowArgumentNullException_WhenDeserializedQueryIsNull()
    {
        // Arrange
        var actor = _fixture.CreateFuturesTickDataQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new GetLastFuturesTickDataParameter(SampleData.EsTickData.ContractId, SampleData.ValueDate);
        var validSubject = new ActorSubject(ActorType.Query, FuturesTickDataQueryActor.ActorName, GetLastFuturesTickDataQuery.Verb, entityId.Format());
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
        var actor = _fixture.CreateFuturesTickDataQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var query = new GetLastFuturesTickDataQuery(SampleData.EsTickData.ContractId, SampleData.ValueDate)
        {
            Subject = new ActorSubject(ActorType.Query, FuturesTickDataQueryActor.ActorName, GetLastFuturesTickDataQuery.Verb, string.Empty),
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
        result.Should().BeOfType<GetLastFuturesTickDataQuery>();
    }

    [Fact]
    public void ParseMessage_ShouldExtractThreadIdFromSubject_Correctly()
    {
        // Arrange
        var actor = _fixture.CreateFuturesTickDataQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new GetLastFuturesTickDataParameter(SampleData.EsTickData.ContractId, SampleData.ValueDate);
        var query = new GetLastFuturesTickDataQuery(entityId.ContractId, entityId.ValueDate)
        {
            Subject = new ActorSubject(ActorType.Query, FuturesTickDataQueryActor.ActorName, GetLastFuturesTickDataQuery.Verb, entityId.Format()),
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
            Arg.Is(GetLastFuturesTickDataQuery.Verb),
            Arg.Any<ActorMessageInfo>());
    }

    [Fact]
    public void ParseMessage_ShouldSetMessageInfoCorrectly_WithAllComponents()
    {
        // Arrange
        var actor = _fixture.CreateFuturesTickDataQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new GetLastFuturesTickDataParameter(SampleData.EsTickData.ContractId, SampleData.ValueDate);
        var query = new GetLastFuturesTickDataQuery(entityId.ContractId, entityId.ValueDate)
        {
            Subject = new ActorSubject(ActorType.Query, FuturesTickDataQueryActor.ActorName, GetLastFuturesTickDataQuery.Verb, entityId.Format()),
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
            Arg.Is<string>(v => v == GetLastFuturesTickDataQuery.Verb),
            Arg.Any<ActorMessageInfo>());
    }

    #endregion

    #region ReceiveAsync Happy Path Tests

    [Fact]
    public async Task ReceiveAsync_ShouldProcessGetLastFuturesTickDataQuery_Successfully()
    {
        // Arrange
        var context = Substitute.For<IQueryActorContext>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        var db = Substitute.For<IMarketDataDbContext>();
        dbFactory.MarketDataDb.Returns(db);
        var actor = _fixture.CreateFuturesTickDataQueryActor(dbFactory: dbFactory);
        var contractId = SampleData.EsTickData.ContractId;
        var valueDate = SampleData.ValueDate;

        db.GetLastFuturesTickDataAsync(contractId, valueDate).Returns(SampleData.EsTickData);

        var query = new GetLastFuturesTickDataQuery(contractId, valueDate)
        {
            Subject = new ActorSubject(ActorType.Query, FuturesTickDataQueryActor.ActorName, GetLastFuturesTickDataQuery.Verb, $"{contractId}.{valueDate:yyyy-MM-dd}"),
        };

        // Act
        await actor.InvokeReceiveAsync(context, query);

        // Assert
        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetLastFuturesTickDataQuery.Verb,
            Arg.Is<ServiceResult<FuturesTickDataV2ReadModel?>>(r => r.Success && r.Value == SampleData.EsTickData));
    }

    [Fact]
    public async Task ReceiveAsync_ShouldProcessGetLastFuturesTickDataByTickDateQuery_Successfully()
    {
        // Arrange
        var context = Substitute.For<IQueryActorContext>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        var db = Substitute.For<IMarketDataDbContext>();
        dbFactory.MarketDataDb.Returns(db);
        var actor = _fixture.CreateFuturesTickDataQueryActor(dbFactory: dbFactory);
        var contractId = SampleData.EsTickData.ContractId;
        var tickDate = new DateTime(2024, 6, 15, 14, 30, 0, DateTimeKind.Utc);
        var entityId = new GetLastFuturesTickDataByTickDateParameter(contractId, tickDate);

        db.GetLastFuturesTickDataByTickDateAsync(contractId, tickDate).Returns(SampleData.EsTickData);

        var query = new GetLastFuturesTickDataByTickDateQuery(contractId, tickDate)
        {
            Subject = new ActorSubject(ActorType.Query, FuturesTickDataQueryActor.ActorName, GetLastFuturesTickDataByTickDateQuery.Verb, entityId.Format()),
        };

        // Act
        await actor.InvokeReceiveAsync(context, query);

        // Assert
        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetLastFuturesTickDataByTickDateQuery.Verb,
            Arg.Is<ServiceResult<FuturesTickDataV2ReadModel?>>(r => r.Success && r.Value == SampleData.EsTickData));
    }

    #endregion

    #region ReceiveAsync Edge Case Tests

    [Fact]
    public async Task ReceiveAsync_ShouldThrowArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var actor = _fixture.CreateFuturesTickDataQueryActor();
        var db = Substitute.For<IMarketDataDbContext>();
        var contractId = SampleData.EsTickData.ContractId;
        var threadId = new ActorThreadId(ActorType.Query, FuturesTickDataQueryActor.ActorName, $"{contractId}.{SampleData.ValueDate:yyyy-MM-dd}");
        var query = new GetLastFuturesTickDataQuery(contractId, SampleData.ValueDate)
        {
            Subject = new ActorSubject(ActorType.Query, FuturesTickDataQueryActor.ActorName, GetLastFuturesTickDataQuery.Verb, $"{contractId}.{SampleData.ValueDate:yyyy-MM-dd}"),
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
        var actor = _fixture.CreateFuturesTickDataQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var db = Substitute.For<IMarketDataDbContext>();
        var contractId = SampleData.EsTickData.ContractId;
        var threadId = new ActorThreadId(ActorType.Query, FuturesTickDataQueryActor.ActorName, $"{contractId}.{SampleData.ValueDate:yyyy-MM-dd}");

        // Act & Assert
        var act = async () => await actor.InvokeReceiveAsync(context, null!);
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("query");
    }

    [Fact]
    public async Task ReceiveAsync_ShouldThrowInvalidOperationException_WhenQueryTypeIsNotSupported()
    {
        // Arrange
        var actor = _fixture.CreateFuturesTickDataQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var db = Substitute.For<IMarketDataDbContext>();
        var contractId = SampleData.EsTickData.ContractId;
        var threadId = new ActorThreadId(ActorType.Query, FuturesTickDataQueryActor.ActorName, $"{contractId}.{SampleData.ValueDate:yyyy-MM-dd}");

        var unsupportedQuery = Substitute.For<IQuery>();
        unsupportedQuery.Subject.Returns(new ActorSubject(ActorType.Query, FuturesTickDataQueryActor.ActorName, "UnsupportedVerb", $"{contractId}.{SampleData.ValueDate:yyyy-MM-dd}"));

        // Act & Assert
        var act = async () => await actor.InvokeReceiveAsync(context, unsupportedQuery);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Unable to process FuturesTickDataQuery query: *");
    }

    #endregion

    #region OnExceptionAsync Happy Path Tests

    [Fact]
    public async Task OnExceptionAsync_ShouldHandleGetLastFuturesTickDataQuery_Exception()
    {
        // Arrange
        var actor = _fixture.CreateFuturesTickDataQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var contractId = SampleData.EsTickData.ContractId;
        var threadId = new ActorThreadId(ActorType.Query, FuturesTickDataQueryActor.ActorName, $"{contractId}.{SampleData.ValueDate:yyyy-MM-dd}");
        var query = new GetLastFuturesTickDataQuery(contractId, SampleData.ValueDate)
        {
            Subject = new ActorSubject(ActorType.Query, FuturesTickDataQueryActor.ActorName, GetLastFuturesTickDataQuery.Verb, $"{contractId}.{SampleData.ValueDate:yyyy-MM-dd}"),
            ErrorCode = 500
        };
        var exception = new Exception("Database connection failed");

        // Act
        await actor.InvokeOnExceptionAsync(context, threadId, query, GetLastFuturesTickDataQuery.Verb, exception);

        // Assert
        await context.Received(1).ReplyAsync(
            threadId,
            GetLastFuturesTickDataQuery.Verb,
            Arg.Is<ServiceResult<FuturesTickDataV2ReadModel?>>(r =>
                !r.Success &&
                r.ErrorCode == 500 &&
                r.ErrorMessage == "Database connection failed"));
    }

    [Fact]
    public async Task OnExceptionAsync_ShouldHandleGetLastFuturesTickDataByTickDateQuery_Exception()
    {
        // Arrange
        var actor = _fixture.CreateFuturesTickDataQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var contractId = SampleData.EsTickData.ContractId;
        var tickDate = new DateTime(2024, 6, 15, 14, 30, 0, DateTimeKind.Utc);
        var entityId = new GetLastFuturesTickDataByTickDateParameter(contractId, tickDate);
        var threadId = new ActorThreadId(ActorType.Query, FuturesTickDataQueryActor.ActorName, entityId.Format());
        var query = new GetLastFuturesTickDataByTickDateQuery(contractId, tickDate)
        {
            Subject = new ActorSubject(ActorType.Query, FuturesTickDataQueryActor.ActorName, GetLastFuturesTickDataByTickDateQuery.Verb, entityId.Format()),
            ErrorCode = 404
        };
        var exception = new Exception("Tick data not found");

        // Act
        await actor.InvokeOnExceptionAsync(context, threadId, query, GetLastFuturesTickDataByTickDateQuery.Verb, exception);

        // Assert
        await context.Received(1).ReplyAsync(
            threadId,
            GetLastFuturesTickDataByTickDateQuery.Verb,
            Arg.Is<ServiceResult<FuturesTickDataV2ReadModel?>>(r =>
                !r.Success &&
                r.ErrorCode == 404 &&
                r.ErrorMessage == "Tick data not found"));
    }

    #endregion

    #region OnExceptionAsync Edge Case Tests

    [Fact]
    public async Task OnExceptionAsync_ShouldThrowArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var actor = _fixture.CreateFuturesTickDataQueryActor();
        var contractId = SampleData.EsTickData.ContractId;
        var threadId = new ActorThreadId(ActorType.Query, FuturesTickDataQueryActor.ActorName, $"{contractId}.{SampleData.ValueDate:yyyy-MM-dd}");
        var query = new GetLastFuturesTickDataQuery(contractId, SampleData.ValueDate)
        {
            Subject = new ActorSubject(ActorType.Query, FuturesTickDataQueryActor.ActorName, GetLastFuturesTickDataQuery.Verb, $"{contractId}.{SampleData.ValueDate:yyyy-MM-dd}"),
        };
        var exception = new Exception("Test exception");

        // Act & Assert
        var act = async () => await actor.InvokeOnExceptionAsync(null!, threadId, query, GetLastFuturesTickDataQuery.Verb, exception);
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("context");
    }

    [Fact]
    public async Task OnExceptionAsync_ShouldThrowArgumentNullException_WhenThreadIdIsNull()
    {
        // Arrange
        var actor = _fixture.CreateFuturesTickDataQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var contractId = SampleData.EsTickData.ContractId;
        var query = new GetLastFuturesTickDataQuery(contractId, SampleData.ValueDate)
        {
            Subject = new ActorSubject(ActorType.Query, FuturesTickDataQueryActor.ActorName, GetLastFuturesTickDataQuery.Verb, $"{contractId}.{SampleData.ValueDate:yyyy-MM-dd}"),
        };
        var exception = new Exception("Test exception");

        // Act & Assert
        var act = async () => await actor.InvokeOnExceptionAsync(context, default, query, GetLastFuturesTickDataQuery.Verb, exception);
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("threadId");
    }

    [Fact]
    public async Task OnExceptionAsync_ShouldThrowArgumentNullException_WhenQueryIsNull()
    {
        // Arrange
        var actor = _fixture.CreateFuturesTickDataQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var contractId = SampleData.EsTickData.ContractId;
        var threadId = new ActorThreadId(ActorType.Query, FuturesTickDataQueryActor.ActorName, $"{contractId}.{SampleData.ValueDate:yyyy-MM-dd}");

        // Act & Assert
        var act = async () => await actor.InvokeOnExceptionAsync(context, threadId, null!, GetLastFuturesTickDataQuery.Verb, null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnExceptionAsync_ShouldThrowArgumentNullException_WhenVerbIsNull()
    {
        // Arrange
        var actor = _fixture.CreateFuturesTickDataQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var contractId = SampleData.EsTickData.ContractId;
        var threadId = new ActorThreadId(ActorType.Query, FuturesTickDataQueryActor.ActorName, $"{contractId}.{SampleData.ValueDate:yyyy-MM-dd}");
        var query = new GetLastFuturesTickDataQuery(contractId, SampleData.ValueDate)
        {
            Subject = new ActorSubject(ActorType.Query, FuturesTickDataQueryActor.ActorName, GetLastFuturesTickDataQuery.Verb, $"{contractId}.{SampleData.ValueDate:yyyy-MM-dd}"),
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
        var actor = _fixture.CreateFuturesTickDataQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var contractId = SampleData.EsTickData.ContractId;
        var threadId = new ActorThreadId(ActorType.Query, FuturesTickDataQueryActor.ActorName, $"{contractId}.{SampleData.ValueDate:yyyy-MM-dd}");

        var unknownQuery = Substitute.For<IQuery>();
        unknownQuery.Subject.Returns(new ActorSubject(ActorType.Query, FuturesTickDataQueryActor.ActorName, "UnknownVerb", $"{contractId}.{SampleData.ValueDate:yyyy-MM-dd}"));
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
        var actor = _fixture.CreateFuturesTickDataQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var contractId = SampleData.EsTickData.ContractId;
        var threadId = new ActorThreadId(ActorType.Query, FuturesTickDataQueryActor.ActorName, $"{contractId}.{SampleData.ValueDate:yyyy-MM-dd}");
        var query = new GetLastFuturesTickDataQuery(contractId, SampleData.ValueDate)
        {
            Subject = new ActorSubject(ActorType.Query, FuturesTickDataQueryActor.ActorName, GetLastFuturesTickDataQuery.Verb, $"{contractId}.{SampleData.ValueDate:yyyy-MM-dd}"),
            ErrorCode = 500
        };
        var innerException = new InvalidOperationException("Inner error");
        var exception = new Exception("Outer error", innerException);

        // Act
        await actor.InvokeOnExceptionAsync(context, threadId, query, GetLastFuturesTickDataQuery.Verb, exception);

        // Assert
        await context.Received(1).ReplyAsync(
            threadId,
            GetLastFuturesTickDataQuery.Verb,
            Arg.Is<ServiceResult<FuturesTickDataV2ReadModel?>>(r =>
                !r.Success &&
                r.ErrorCode == 500 &&
                r.ErrorMessage == "Outer error"));
    }

    [Fact]
    public async Task OnExceptionAsync_ShouldHandleExceptionWhenReplyAsyncThrows()
    {
        // Arrange
        var actor = _fixture.CreateFuturesTickDataQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var contractId = SampleData.EsTickData.ContractId;
        var threadId = new ActorThreadId(ActorType.Query, FuturesTickDataQueryActor.ActorName, $"{contractId}.{SampleData.ValueDate:yyyy-MM-dd}");
        var query = new GetLastFuturesTickDataQuery(contractId, SampleData.ValueDate)
        {
            Subject = new ActorSubject(ActorType.Query, FuturesTickDataQueryActor.ActorName, GetLastFuturesTickDataQuery.Verb, $"{contractId}.{SampleData.ValueDate:yyyy-MM-dd}"),
            ErrorCode = 500
        };
        var exception = new Exception("Original error");

        context.ReplyAsync(threadId, GetLastFuturesTickDataQuery.Verb, Arg.Any<ServiceResult<FuturesTickDataV2ReadModel?>>())
            .Returns<ValueTask>(_ => throw new InvalidOperationException("Reply failed"));

        // Act - should not throw, should be caught and logged
        var act = async () => await actor.InvokeOnExceptionAsync(context, threadId, query, GetLastFuturesTickDataQuery.Verb, exception);
        await act.Should().NotThrowAsync();

        // Assert - verify ReplyAsync was called (and it threw, which was caught)
        await context.Received(1).ReplyAsync(
            threadId,
            GetLastFuturesTickDataQuery.Verb,
            Arg.Any<ServiceResult<FuturesTickDataV2ReadModel?>>());
    }

    #endregion

}
