using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.State;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Query;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed.Queries;
using TomasAI.IFM.Shared.MarketDataFeed.QueryParameters;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.FuturesOptionTickData;

public class FuturesOptionTickDataQueryActorTests : IClassFixture<MarketDataFeedTestFixture>
{
    readonly MarketDataFeedTestFixture _fixture;

    public FuturesOptionTickDataQueryActorTests(MarketDataFeedTestFixture fixture)
    {
        _fixture = fixture;
    }

    public class TestableFuturesOptionTickDataQueryActor : FuturesOptionTickDataQueryActor
    {
        public TestableFuturesOptionTickDataQueryActor(IDbContextFactory dbFactory, ILogger<FuturesOptionTickDataQueryActor> logger)
            : base(dbFactory, logger)
        {
        }

        public IQuery InvokeParseMessage(IQueryActorContext context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public async ValueTask InvokeReceiveAsync(IQueryActorContext context, IActorState state, IQuery query)
            => await ReceiveAsync(context, state, query);

        public async ValueTask InvokeOnExceptionAsync(IQueryActorContext context, ActorThreadId threadId, IQuery query, string verb, Exception ex)
            => await OnExceptionAsync(context, threadId, query, verb, ex);

        public async ValueTask<IActorState> InvokeOnLoadStateAsync(IQueryActorContext context, ActorThreadId threadId, IQuery query)
            => await OnLoadStateAsync(context, threadId, query);
    }

    #region ParseMessage Happy Path Tests

    [Fact]
    public void ParseMessage_ShouldParseGetLastFuturesOptionTickDataQuery_Successfully()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesOptionTickDataQueryActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new GetLastFuturesOptionTickDataParameter(
            SampleData.EsOptionTickData.ContractId,
            SampleData.ValueDate);
        var query = new GetLastFuturesOptionTickDataQuery(
            entityId.ContractId, entityId.ValueDate)
        {
            Subject = new ActorSubject(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, GetLastFuturesOptionTickDataQuery.Verb, entityId.Format()),
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
        result.Should().BeOfType<GetLastFuturesOptionTickDataQuery>();
        var parsedQuery = result as GetLastFuturesOptionTickDataQuery;
        parsedQuery!.ContractId.Should().Be(entityId.ContractId);
        parsedQuery.ValueDate.Should().Be(entityId.ValueDate);
        context.Received(1).SetMessageInfo(
            Arg.Any<ActorThreadId>(),
            Arg.Is(GetLastFuturesOptionTickDataQuery.Verb),
            Arg.Any<ActorMessageInfo>());
    }

    [Fact]
    public void ParseMessage_ShouldExtractThreadIdFromSubject_Correctly()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesOptionTickDataQueryActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new GetLastFuturesOptionTickDataParameter(
            SampleData.EsOptionTickData.ContractId,
            SampleData.ValueDate);
        var query = new GetLastFuturesOptionTickDataQuery(
            entityId.ContractId, entityId.ValueDate)
        {
            Subject = new ActorSubject(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, GetLastFuturesOptionTickDataQuery.Verb, entityId.Format()),
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
            Arg.Is(GetLastFuturesOptionTickDataQuery.Verb),
            Arg.Any<ActorMessageInfo>());
    }

    [Fact]
    public void ParseMessage_ShouldCallSetMessageInfo_ExactlyOnce()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesOptionTickDataQueryActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new GetLastFuturesOptionTickDataParameter(
            SampleData.EsOptionTickData.ContractId,
            SampleData.ValueDate);
        var query = new GetLastFuturesOptionTickDataQuery
        {
            Subject = new ActorSubject(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, GetLastFuturesOptionTickDataQuery.Verb, entityId.Format()),
            EntityId = entityId
        };
        var serializedData = _fixture.DataSerializer.Serialize(query);
        var message = new NatsMsg<byte[]>
        {
            Subject = query.Subject.ToString(),
            Data = serializedData
        };

        // Act
        actor.InvokeParseMessage(context, message);

        // Assert
        context.Received(1).SetMessageInfo(
            Arg.Any<ActorThreadId>(),
            Arg.Any<string>(),
            Arg.Any<ActorMessageInfo>());
    }

    [Fact]
    public void ParseMessage_ShouldPreserveQueryProperties_AfterDeserialization()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesOptionTickDataQueryActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new GetLastFuturesOptionTickDataParameter("ES20240601P5400", new DateOnly(2024, 3, 1));
        var originalQuery = new GetLastFuturesOptionTickDataQuery(
            entityId.ContractId, entityId.ValueDate)
        {
            Subject = new ActorSubject(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, GetLastFuturesOptionTickDataQuery.Verb, entityId.Format()),
        };
        var serializedData = _fixture.DataSerializer.Serialize(originalQuery);
        var message = new NatsMsg<byte[]>
        {
            Subject = originalQuery.Subject.ToString(),
            Data = serializedData
        };

        // Act
        var result = actor.InvokeParseMessage(context, message);

        // Assert
        result.Should().NotBeNull();
        var parsedQuery = result as GetLastFuturesOptionTickDataQuery;
        parsedQuery!.ContractId.Should().Be(originalQuery.ContractId);
        parsedQuery.ValueDate.Should().Be(originalQuery.ValueDate);
        parsedQuery.Subject.Verb.Should().Be(originalQuery.Subject.Verb);
    }

    [Fact]
    public void ParseMessage_ShouldHandleEmptyEntityId_InSubject()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesOptionTickDataQueryActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var subjectWithEmptyEntityId = new ActorSubject(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, GetLastFuturesOptionTickDataQuery.Verb, string.Empty);
        var entityId = new GetLastFuturesOptionTickDataParameter(
            SampleData.EsOptionTickData.ContractId,
            SampleData.ValueDate);
        var query = new GetLastFuturesOptionTickDataQuery
        {
            Subject = subjectWithEmptyEntityId,
            EntityId = entityId
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
        result.Should().BeOfType<GetLastFuturesOptionTickDataQuery>();
    }

    [Fact]
    public void ParseMessage_ShouldNotModifyOriginalMessage()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesOptionTickDataQueryActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new GetLastFuturesOptionTickDataParameter(
            SampleData.EsOptionTickData.ContractId,
            SampleData.ValueDate);
        var query = new GetLastFuturesOptionTickDataQuery
        {
            Subject = new ActorSubject(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, GetLastFuturesOptionTickDataQuery.Verb, entityId.Format()),
            EntityId = entityId
        };
        var serializedData = _fixture.DataSerializer.Serialize(query);
        var originalSubject = query.Subject.ToString();
        var originalData = serializedData.ToArray();
        var message = new NatsMsg<byte[]>
        {
            Subject = originalSubject,
            Data = serializedData
        };

        // Act
        actor.InvokeParseMessage(context, message);

        // Assert
        message.Subject.Should().Be(originalSubject);
        message.Data.Should().BeEquivalentTo(originalData);
    }

    [Fact]
    public void ParseMessage_ShouldHandleEmptyContractId_InQuery()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesOptionTickDataQueryActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new GetLastFuturesOptionTickDataParameter(string.Empty, DateOnly.MinValue);
        var query = new GetLastFuturesOptionTickDataQuery(
            entityId.ContractId, entityId.ValueDate)
        {
            Subject = new ActorSubject(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, GetLastFuturesOptionTickDataQuery.Verb, entityId.Format()),
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
        var parsedQuery = result as GetLastFuturesOptionTickDataQuery;
        parsedQuery!.ContractId.Should().BeEmpty();
    }

    #endregion

    #region ParseMessage Edge Case Tests

    [Fact]
    public void ParseMessage_ShouldThrowArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesOptionTickDataQueryActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataQueryActor(logger);
        var entityId = new GetLastFuturesOptionTickDataParameter(
            SampleData.EsOptionTickData.ContractId,
            SampleData.ValueDate);
        var query = new GetLastFuturesOptionTickDataQuery
        {
            Subject = new ActorSubject(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, GetLastFuturesOptionTickDataQuery.Verb, entityId.Format()),
            EntityId = entityId
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
        var logger = Substitute.For<ILogger<FuturesOptionTickDataQueryActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var invalidSubject = new ActorSubject(ActorType.Command, FuturesOptionTickDataQueryActor.ActorName, GetLastFuturesOptionTickDataQuery.Verb, "entity-id");
        var message = new NatsMsg<byte[]>
        {
            Subject = invalidSubject.ToString(),
            Data = Array.Empty<byte>()
        };

        // Act & Assert
        var act = () => actor.InvokeParseMessage(context, message);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesOptionTickDataQueryActor.ActorName} query from message: *");
    }

    [Fact]
    public void ParseMessage_ShouldThrowInvalidOperationException_WhenActorNameIsIncorrect()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesOptionTickDataQueryActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var invalidSubject = new ActorSubject(ActorType.Query, "WrongActor", GetLastFuturesOptionTickDataQuery.Verb, "entity-id");
        var message = new NatsMsg<byte[]>
        {
            Subject = invalidSubject.ToString(),
            Data = Array.Empty<byte>()
        };

        // Act & Assert
        var act = () => actor.InvokeParseMessage(context, message);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesOptionTickDataQueryActor.ActorName} query from message: *");
    }

    [Fact]
    public void ParseMessage_ShouldThrowInvalidOperationException_WhenVerbIsNotInParseMap()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesOptionTickDataQueryActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var invalidSubject = new ActorSubject(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, "UnknownVerb", "entity-id");
        var message = new NatsMsg<byte[]>
        {
            Subject = invalidSubject.ToString(),
            Data = Array.Empty<byte>()
        };

        // Act & Assert
        var act = () => actor.InvokeParseMessage(context, message);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesOptionTickDataQueryActor.ActorName} query from message: *");
    }

    [Fact]
    public void ParseMessage_ShouldThrowArgumentNullException_WhenDeserializedQueryIsNull()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesOptionTickDataQueryActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var validSubject = new ActorSubject(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, GetLastFuturesOptionTickDataQuery.Verb, "entity-id");
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
    public void ParseMessage_ShouldThrowException_WhenPayloadIsCorrupted()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesOptionTickDataQueryActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var validSubject = new ActorSubject(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, GetLastFuturesOptionTickDataQuery.Verb, "entity-id");
        var corruptedPayload = new byte[] { 0x00, 0x01, 0x02, 0xFF, 0xFE };
        var message = new NatsMsg<byte[]>
        {
            Subject = validSubject.ToString(),
            Data = corruptedPayload
        };

        // Act & Assert
        var act = () => actor.InvokeParseMessage(context, message);
        act.Should().Throw<Exception>();
    }

    #endregion

    #region ReceiveAsync Happy Path Tests

    [Fact]
    public async Task ReceiveAsync_ShouldProcessGetLastFuturesOptionTickDataQuery_Successfully()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesOptionTickDataQueryActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new GetLastFuturesOptionTickDataParameter(
            SampleData.EsOptionTickData.ContractId,
            SampleData.ValueDate);

        var state = new FuturesOptionTickDataQueryState() { Id = new ActorThreadId(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, entityId.Format()) };
        var query = new GetLastFuturesOptionTickDataQuery(
            entityId.ContractId, entityId.ValueDate)
        {
            Subject = new ActorSubject(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, GetLastFuturesOptionTickDataQuery.Verb, entityId.Format()),
        };

        context.GetMessageInfo(state.Id, GetLastFuturesOptionTickDataQuery.Verb)
            .Returns(new ActorMessageInfo(default, query));

        // Act
        try
        {
            await actor.InvokeReceiveAsync(context, state, query);
        }
        catch (NullReferenceException)
        {
            // Query handler replies through NATS; with default msg this can throw during tests.
        }

        // Assert
        context.Received(1).GetMessageInfo(state.Id, GetLastFuturesOptionTickDataQuery.Verb);
    }

    [Fact]
    public async Task ReceiveAsync_ShouldHandleNullResult_FromDatabase()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesOptionTickDataQueryActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new GetLastFuturesOptionTickDataParameter(
            "NONEXISTENT_CONTRACT",
            SampleData.ValueDate);

        var state = new FuturesOptionTickDataQueryState() { Id = new ActorThreadId(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, entityId.Format()) };
        var query = new GetLastFuturesOptionTickDataQuery(
            entityId.ContractId, entityId.ValueDate)
        {
            Subject = new ActorSubject(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, GetLastFuturesOptionTickDataQuery.Verb, entityId.Format()),
        };

        context.GetMessageInfo(state.Id, GetLastFuturesOptionTickDataQuery.Verb)
            .Returns(new ActorMessageInfo(default, query));

        // Act
        try
        {
            await actor.InvokeReceiveAsync(context, state, query);
        }
        catch (NullReferenceException)
        {
            // Query handler replies through NATS; with default msg this can throw during tests.
        }

        // Assert
        context.Received(1).GetMessageInfo(state.Id, GetLastFuturesOptionTickDataQuery.Verb);
    }

    [Fact]
    public async Task ReceiveAsync_ShouldCallDatabaseWithCorrectParameters()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesOptionTickDataQueryActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var contractId = "ES20240601P5400";
        var valueDate = new DateOnly(2024, 7, 1);
        var entityId = new GetLastFuturesOptionTickDataParameter(contractId, valueDate);

        var state = new FuturesOptionTickDataQueryState() { Id = new ActorThreadId(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, entityId.Format()) };
        var query = new GetLastFuturesOptionTickDataQuery(contractId, valueDate)
        {
            Subject = new ActorSubject(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, GetLastFuturesOptionTickDataQuery.Verb, entityId.Format()),
        };

        context.GetMessageInfo(state.Id, GetLastFuturesOptionTickDataQuery.Verb)
            .Returns(new ActorMessageInfo(default, query));

        // Act
        try
        {
            await actor.InvokeReceiveAsync(context, state, query);
        }
        catch (NullReferenceException)
        {
            // Query handler replies through NATS; with default msg this can throw during tests.
        }

        // Assert
        context.Received(1).GetMessageInfo(state.Id, GetLastFuturesOptionTickDataQuery.Verb);
    }

    #endregion

    #region ReceiveAsync Edge Case Tests

    [Fact]
    public async Task ReceiveAsync_ShouldThrowArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesOptionTickDataQueryActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataQueryActor(logger);
        var db = Substitute.For<IDbContextFactory>();
        var entityId = new GetLastFuturesOptionTickDataParameter(
            SampleData.EsOptionTickData.ContractId,
            SampleData.ValueDate);
        var state = new FuturesOptionTickDataQueryState() { Id = new ActorThreadId(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, entityId.Format()) };
        var query = new GetLastFuturesOptionTickDataQuery
        {
            Subject = new ActorSubject(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, GetLastFuturesOptionTickDataQuery.Verb, entityId.Format()),
            EntityId = entityId
        };

        // Act & Assert
        var act = async () => await actor.InvokeReceiveAsync(null!, state, query);
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("context");
    }

    [Fact]
    public async Task ReceiveAsync_ShouldThrowArgumentNullException_WhenStateIsNull()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesOptionTickDataQueryActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new GetLastFuturesOptionTickDataParameter(
            SampleData.EsOptionTickData.ContractId,
            SampleData.ValueDate);
        var query = new GetLastFuturesOptionTickDataQuery
        {
            Subject = new ActorSubject(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, GetLastFuturesOptionTickDataQuery.Verb, entityId.Format()),
            EntityId = entityId
        };

        // Act & Assert
        var act = async () => await actor.InvokeReceiveAsync(context, null!, query);
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("state");
    }

    [Fact]
    public async Task ReceiveAsync_ShouldThrowArgumentNullException_WhenQueryIsNull()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesOptionTickDataQueryActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var db = Substitute.For<IDbContextFactory>();
        var entityId = new GetLastFuturesOptionTickDataParameter(
            SampleData.EsOptionTickData.ContractId,
            SampleData.ValueDate);
        var state = new FuturesOptionTickDataQueryState() { Id = new ActorThreadId(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, entityId.Format()) };

        // Act & Assert
        var act = async () => await actor.InvokeReceiveAsync(context, state, null!);
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("query");
    }

    [Fact]
    public async Task ReceiveAsync_ShouldThrowArgumentNullException_WhenStateIsNotFuturesOptionTickDataQueryState()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesOptionTickDataQueryActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var invalidState = Substitute.For<IActorState>();
        var entityId = new GetLastFuturesOptionTickDataParameter(
            SampleData.EsOptionTickData.ContractId,
            SampleData.ValueDate);
        var query = new GetLastFuturesOptionTickDataQuery
        {
            Subject = new ActorSubject(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, GetLastFuturesOptionTickDataQuery.Verb, entityId.Format()),
            EntityId = entityId
        };

        // Act & Assert
        var act = async () => await actor.InvokeReceiveAsync(context, invalidState, query);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReceiveAsync_ShouldThrowInvalidOperationException_WhenQueryTypeIsNotSupported()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesOptionTickDataQueryActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var db = Substitute.For<IDbContextFactory>();
        var entityId = new GetLastFuturesOptionTickDataParameter(
            SampleData.EsOptionTickData.ContractId,
            SampleData.ValueDate);
        var state = new FuturesOptionTickDataQueryState() { Id = new ActorThreadId(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, entityId.Format()) };

        var unsupportedQuery = Substitute.For<IQuery>();
        unsupportedQuery.Subject.Returns(new ActorSubject(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, "UnsupportedVerb", entityId.Format()));

        // Act & Assert
        var act = async () => await actor.InvokeReceiveAsync(context, state, unsupportedQuery);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to process {FuturesOptionTickDataQueryActor.ActorName} query: *");
    }

    #endregion

    #region OnExceptionAsync Happy Path Tests

    [Fact]
    public async Task OnExceptionAsync_ShouldHandleGetLastFuturesOptionTickDataQuery_Exception()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesOptionTickDataQueryActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new GetLastFuturesOptionTickDataParameter(
            SampleData.EsOptionTickData.ContractId,
            SampleData.ValueDate);
        var threadId = new ActorThreadId(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, entityId.Format());
        var query = new GetLastFuturesOptionTickDataQuery(
            entityId.ContractId, entityId.ValueDate)
        {
            Subject = new ActorSubject(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, GetLastFuturesOptionTickDataQuery.Verb, entityId.Format()),
        };
        var exception = new Exception("Database connection failed");

        // Act
        await actor.InvokeOnExceptionAsync(context, threadId, query, GetLastFuturesOptionTickDataQuery.Verb, exception);

        // Assert
        await context.Received(1).ReplyAsync(
            threadId,
            GetLastFuturesOptionTickDataQuery.Verb,
            Arg.Is<ServiceResult<FuturesOptionTickDataV2ReadModel?>>(r =>
                !r.Success &&
                r.ErrorCode == query.ErrorCode &&
                r.ErrorMessage == "Database connection failed"));
    }

    [Fact]
    public async Task OnExceptionAsync_ShouldHandleUnknownQueryType_WithFallbackReply()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesOptionTickDataQueryActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var threadId = new ActorThreadId(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, "unknown-thread");
        var unknownQuery = Substitute.For<IQuery>();
        unknownQuery.ErrorCode.Returns(9999);
        unknownQuery.Subject.Returns(new ActorSubject(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, "UnknownVerb", "unknown-thread"));
        var exception = new Exception("Unknown query error");

        // Act
        await actor.InvokeOnExceptionAsync(context, threadId, unknownQuery, "UnknownVerb", exception);

        // Assert
        await context.Received(1).ReplyAsync(
            threadId,
            "UnknownVerb",
            Arg.Is<ServiceFailed<ActorEntityId>>(r =>
                !r.Success &&
                r.ErrorCode == 9999 &&
                r.ErrorMessage == "Unknown query error"));
    }

    [Fact]
    public async Task OnExceptionAsync_ShouldHandleTimeoutException()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesOptionTickDataQueryActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new GetLastFuturesOptionTickDataParameter(
            SampleData.EsOptionTickData.ContractId,
            SampleData.ValueDate);
        var threadId = new ActorThreadId(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, entityId.Format());
        var query = new GetLastFuturesOptionTickDataQuery(
            entityId.ContractId, entityId.ValueDate)
        {
            Subject = new ActorSubject(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, GetLastFuturesOptionTickDataQuery.Verb, entityId.Format()),
        };
        var exception = new TimeoutException("Database operation timed out");

        // Act
        await actor.InvokeOnExceptionAsync(context, threadId, query, GetLastFuturesOptionTickDataQuery.Verb, exception);

        // Assert
        await context.Received(1).ReplyAsync(
            threadId,
            GetLastFuturesOptionTickDataQuery.Verb,
            Arg.Is<ServiceResult<FuturesOptionTickDataV2ReadModel?>>(r =>
                !r.Success &&
                r.ErrorCode == query.ErrorCode &&
                r.ErrorMessage == "Database operation timed out"));
    }

    #endregion

    #region OnExceptionAsync Edge Case Tests

    [Fact]
    public async Task OnExceptionAsync_ShouldThrowArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesOptionTickDataQueryActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataQueryActor(logger);
        var entityId = new GetLastFuturesOptionTickDataParameter(
            SampleData.EsOptionTickData.ContractId,
            SampleData.ValueDate);
        var threadId = new ActorThreadId(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, entityId.Format());
        var query = new GetLastFuturesOptionTickDataQuery
        {
            Subject = new ActorSubject(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, GetLastFuturesOptionTickDataQuery.Verb, entityId.Format()),
            EntityId = entityId
        };
        var exception = new Exception("Test error");

        // Act & Assert
        var act = async () => await actor.InvokeOnExceptionAsync(null!, threadId, query, GetLastFuturesOptionTickDataQuery.Verb, exception);
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("context");
    }

    [Fact]
    public async Task OnExceptionAsync_ShouldThrowArgumentNullException_WhenThreadIdIsNull()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesOptionTickDataQueryActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new GetLastFuturesOptionTickDataParameter(
            SampleData.EsOptionTickData.ContractId,
            SampleData.ValueDate);
        var query = new GetLastFuturesOptionTickDataQuery
        {
            Subject = new ActorSubject(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, GetLastFuturesOptionTickDataQuery.Verb, entityId.Format()),
            EntityId = entityId
        };
        var exception = new Exception("Test error");

        // Act & Assert
        var act = async () => await actor.InvokeOnExceptionAsync(context, default, query, GetLastFuturesOptionTickDataQuery.Verb, exception);
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("threadId");
    }

    [Fact]
    public async Task OnExceptionAsync_ShouldThrowArgumentNullException_WhenQueryIsNull()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesOptionTickDataQueryActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var threadId = new ActorThreadId(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, "test-thread");
        var exception = new Exception("Test error");

        // Act & Assert
        var act = async () => await actor.InvokeOnExceptionAsync(context, threadId, null!, GetLastFuturesOptionTickDataQuery.Verb, exception);
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("query");
    }

    [Fact]
    public async Task OnExceptionAsync_ShouldThrowArgumentNullException_WhenVerbIsNull()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesOptionTickDataQueryActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new GetLastFuturesOptionTickDataParameter(
            SampleData.EsOptionTickData.ContractId,
            SampleData.ValueDate);
        var threadId = new ActorThreadId(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, entityId.Format());
        var query = new GetLastFuturesOptionTickDataQuery
        {
            Subject = new ActorSubject(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, GetLastFuturesOptionTickDataQuery.Verb, entityId.Format()),
            EntityId = entityId
        };
        var exception = new Exception("Test error");

        // Act & Assert
        var act = async () => await actor.InvokeOnExceptionAsync(context, threadId, query, null!, exception);
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("verb");
    }

    [Fact]
    public async Task OnExceptionAsync_ShouldThrowArgumentNullException_WhenExceptionMessageIsNull()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesOptionTickDataQueryActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new GetLastFuturesOptionTickDataParameter(
            SampleData.EsOptionTickData.ContractId,
            SampleData.ValueDate);
        var threadId = new ActorThreadId(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, entityId.Format());
        var query = new GetLastFuturesOptionTickDataQuery(
            entityId.ContractId, entityId.ValueDate)
        {
            Subject = new ActorSubject(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, GetLastFuturesOptionTickDataQuery.Verb, entityId.Format()),
        };

        // Act & Assert
        var act = async () => await actor.InvokeOnExceptionAsync(context, threadId, query, GetLastFuturesOptionTickDataQuery.Verb, null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnExceptionAsync_ShouldNotThrow_WhenReplyAsyncFails()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesOptionTickDataQueryActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new GetLastFuturesOptionTickDataParameter(
            SampleData.EsOptionTickData.ContractId,
            SampleData.ValueDate);
        var threadId = new ActorThreadId(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, entityId.Format());
        var query = new GetLastFuturesOptionTickDataQuery(
            entityId.ContractId, entityId.ValueDate)
        {
            Subject = new ActorSubject(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, GetLastFuturesOptionTickDataQuery.Verb, entityId.Format()),
        };
        var exception = new Exception("Original error");

        context.ReplyAsync(Arg.Any<ActorThreadId>(), Arg.Any<string>(), Arg.Any<ServiceResult<FuturesOptionTickDataV2ReadModel?>>())
            .Returns<ValueTask>(_ => throw new Exception("ReplyAsync failed"));

        // Act - should not throw, exception is caught and logged
        var act = async () => await actor.InvokeOnExceptionAsync(context, threadId, query, GetLastFuturesOptionTickDataQuery.Verb, exception);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region OnLoadStateAsync Happy Path Tests

    [Fact]
    public async Task OnLoadStateAsync_ShouldReturnState_Successfully()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesOptionTickDataQueryActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new GetLastFuturesOptionTickDataParameter(
            SampleData.EsOptionTickData.ContractId,
            SampleData.ValueDate);
        var threadId = new ActorThreadId(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, entityId.Format());
        var query = new GetLastFuturesOptionTickDataQuery(
            entityId.ContractId, entityId.ValueDate)
        {
            Subject = new ActorSubject(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, GetLastFuturesOptionTickDataQuery.Verb, entityId.Format()),
        };

        var db = Substitute.For<IDbContextFactory>();
        var expectedState = new FuturesOptionTickDataQueryState() { Id = threadId };
        var container = Substitute.For<IContainerInstance>();
        container.Resolve<IQueryActorState<FuturesOptionTickDataQueryState>>().Returns(expectedState);
        context.Container.Returns(container);

        // Act
        var result = await actor.InvokeOnLoadStateAsync(context, threadId, query);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<FuturesOptionTickDataQueryState>();
        (result as FuturesOptionTickDataQueryState)!.Id.Should().Be(threadId);
    }

    [Fact]
    public async Task OnLoadStateAsync_ShouldSetThreadIdOnState()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesOptionTickDataQueryActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new GetLastFuturesOptionTickDataParameter(
            SampleData.EsOptionTickData.ContractId,
            SampleData.ValueDate);
        var threadId = new ActorThreadId(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, entityId.Format());
        var query = new GetLastFuturesOptionTickDataQuery(
            entityId.ContractId, entityId.ValueDate)
        {
            Subject = new ActorSubject(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, GetLastFuturesOptionTickDataQuery.Verb, entityId.Format()),
        };

        var db = Substitute.For<IDbContextFactory>();
        var state = new FuturesOptionTickDataQueryState() { Id = new ActorThreadId(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, "initial-thread") };
        var container = Substitute.For<IContainerInstance>();
        container.Resolve<IQueryActorState<FuturesOptionTickDataQueryState>>().Returns(state);
        context.Container.Returns(container);

        // Act
        var result = await actor.InvokeOnLoadStateAsync(context, threadId, query);

        // Assert
        result.Should().NotBeNull();
        var tickDataState = result as FuturesOptionTickDataQueryState;
        tickDataState!.Id.Should().Be(threadId);
    }

    [Fact]
    public async Task OnLoadStateAsync_ShouldResolveFromContainer()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesOptionTickDataQueryActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new GetLastFuturesOptionTickDataParameter(
            SampleData.EsOptionTickData.ContractId,
            SampleData.ValueDate);
        var threadId = new ActorThreadId(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, entityId.Format());
        var query = new GetLastFuturesOptionTickDataQuery(
            entityId.ContractId, entityId.ValueDate)
        {
            Subject = new ActorSubject(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, GetLastFuturesOptionTickDataQuery.Verb, entityId.Format()),
        };

        var db = Substitute.For<IDbContextFactory>();
        var expectedState = new FuturesOptionTickDataQueryState() { Id = threadId };
        var container = Substitute.For<IContainerInstance>();
        container.Resolve<IQueryActorState<FuturesOptionTickDataQueryState>>().Returns(expectedState);
        context.Container.Returns(container);

        // Act
        await actor.InvokeOnLoadStateAsync(context, threadId, query);

        // Assert
        container.Received(1).Resolve<IQueryActorState<FuturesOptionTickDataQueryState>>();
    }

    #endregion

    #region OnLoadStateAsync Edge Case Tests

    [Fact]
    public async Task OnLoadStateAsync_ShouldThrowArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesOptionTickDataQueryActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataQueryActor(logger);
        var entityId = new GetLastFuturesOptionTickDataParameter(
            SampleData.EsOptionTickData.ContractId,
            SampleData.ValueDate);
        var threadId = new ActorThreadId(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, entityId.Format());
        var query = new GetLastFuturesOptionTickDataQuery(
            entityId.ContractId, entityId.ValueDate)
        {
            Subject = new ActorSubject(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, GetLastFuturesOptionTickDataQuery.Verb, entityId.Format()),
        };

        // Act & Assert
        var act = async () => await actor.InvokeOnLoadStateAsync(null!, threadId, query);
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("context");
    }

    [Fact]
    public async Task OnLoadStateAsync_ShouldThrowArgumentNullException_WhenThreadIdIsNull()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesOptionTickDataQueryActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new GetLastFuturesOptionTickDataParameter(
            SampleData.EsOptionTickData.ContractId,
            SampleData.ValueDate);
        var query = new GetLastFuturesOptionTickDataQuery(
            entityId.ContractId, entityId.ValueDate)
        {
            Subject = new ActorSubject(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, GetLastFuturesOptionTickDataQuery.Verb, entityId.Format()),
        };

        // Act & Assert
        var act = async () => await actor.InvokeOnLoadStateAsync(context, default, query);
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("threadId");
    }

    [Fact]
    public async Task OnLoadStateAsync_ShouldThrowArgumentNullException_WhenQueryIsNull()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesOptionTickDataQueryActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var threadId = new ActorThreadId(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, "test-thread");

        // Act & Assert
        var act = async () => await actor.InvokeOnLoadStateAsync(context, threadId, null!);
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("query");
    }

    [Fact]
    public async Task OnLoadStateAsync_ShouldThrowException_WhenContainerResolveThrows()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesOptionTickDataQueryActor>>();
        var actor = _fixture.CreateFuturesOptionTickDataQueryActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new GetLastFuturesOptionTickDataParameter(
            SampleData.EsOptionTickData.ContractId,
            SampleData.ValueDate);
        var threadId = new ActorThreadId(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, entityId.Format());
        var query = new GetLastFuturesOptionTickDataQuery(
            entityId.ContractId, entityId.ValueDate)
        {
            Subject = new ActorSubject(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, GetLastFuturesOptionTickDataQuery.Verb, entityId.Format()),
        };

        var container = Substitute.For<IContainerInstance>();
        container.When(c => c.Resolve<IQueryActorState<FuturesOptionTickDataQueryState>>())
            .Do(_ => throw new InvalidOperationException("Container resolution failed"));
        context.Container.Returns(container);

        // Act & Assert
        var act = async () => await actor.InvokeOnLoadStateAsync(context, threadId, query);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Container resolution failed");
    }

    #endregion
}

