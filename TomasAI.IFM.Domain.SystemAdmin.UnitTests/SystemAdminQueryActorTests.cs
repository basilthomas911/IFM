using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Domain.SystemAdmin.Actor.Query;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.SystemAdmin;
using TomasAI.IFM.Shared.SystemAdmin.Queries;
using TomasAI.IFM.Shared.SystemAdmin.ViewModels;

namespace TomasAI.IFM.Domain.SystemAdmin.Actor.UnitTests;

public class SystemAdminQueryActorTests : IClassFixture<SystemAdminFixture>
{
    readonly SystemAdminFixture _fixture;

    public SystemAdminQueryActorTests(SystemAdminFixture fixture)
    {
        _fixture = fixture;
    }

    public class TestableSystemAdminQueryActor(ILogger<SystemAdminQueryActor> logger)
        : SystemAdminQueryActor(logger)
    {
        public IQuery InvokeParseMessage(IQueryActorContext context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public async ValueTask InvokeReceiveAsync(IQueryActorContext context, IQuery query)
            => await ReceiveAsync(context, query);


        public async ValueTask InvokeOnExceptionAsync(IQueryActorContext context, ActorThreadId threadId, IQuery query, string verb, Exception ex)
            => await OnExceptionAsync(context, threadId, query, verb, ex);
    }

    #region Helper

    static GetDatabaseNamesQuery CreateQuery(string? entityId = null)
    {
        var id = entityId ?? ActorEntityId.Default.Format();
        return new GetDatabaseNamesQuery
        {
            Subject = new ActorSubject(ActorType.Query, GetDatabaseNamesQuery.Actor, GetDatabaseNamesQuery.Verb, id),
            EntityId = new ActorEntityId(id)
        };
    }

    #endregion

    #region ParseMessage Happy Path Tests

    [Fact]
    public void ParseMessage_DeserializesGetDatabaseNamesQuery_Successfully()
    {
        // Arrange
        _fixture.DataSerializer.Should().NotBeNull();
        _fixture.MsgSerializer.Should().NotBeNull();

        var logger = Substitute.For<ILogger<SystemAdminQueryActor>>();
        var actor = _fixture.CreateQueryActor(logger);

        var query = CreateQuery();

        var payload = ActorExtensions.DataSerializer.Serialize(query);
        var subject = query.Subject.ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<IQueryActorContext>();

        // Act
        var result = actor.InvokeParseMessage(context, natsMsg);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<GetDatabaseNamesQuery>();
        var deserializedQuery = result as GetDatabaseNamesQuery;
        deserializedQuery.Should().NotBeNull();
        deserializedQuery!.Subject.ToString().Should().Be(subject);
    }

    [Fact]
    public void ParseMessage_CallsSetMessageInfo_ExactlyOnce()
    {
        // Arrange
        var logger = Substitute.For<ILogger<SystemAdminQueryActor>>();
        var actor = _fixture.CreateQueryActor(logger);

        var query = CreateQuery();

        var payload = ActorExtensions.DataSerializer.Serialize(query);
        var subject = query.Subject.ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<IQueryActorContext>();

        // Act
        actor.InvokeParseMessage(context, natsMsg);

        // Assert
        context.Received(1).SetMessageInfo(
            Arg.Any<ActorThreadId>(),
            Arg.Is(GetDatabaseNamesQuery.Verb),
            Arg.Any<ActorMessageInfo>());
    }

    [Fact]
    public void ParseMessage_PreservesSubjectAfterSerialization()
    {
        // Arrange
        var logger = Substitute.For<ILogger<SystemAdminQueryActor>>();
        var actor = _fixture.CreateQueryActor(logger);

        var query = CreateQuery();
        var originalSubject = query.Subject.ToString();

        var payload = ActorExtensions.DataSerializer.Serialize(query);
        var natsMsg = new NatsMsg<byte[]>(originalSubject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<IQueryActorContext>();

        // Act
        var result = actor.InvokeParseMessage(context, natsMsg);

        // Assert
        result.Should().NotBeNull();
        var parsedQuery = result as GetDatabaseNamesQuery;
        parsedQuery!.Subject.Verb.Should().Be(GetDatabaseNamesQuery.Verb);
        parsedQuery.Subject.Name.Should().Be(GetDatabaseNamesQuery.Actor);
    }

    [Fact]
    public void ParseMessage_ExtractsThreadIdFromSubject_Correctly()
    {
        // Arrange
        var logger = Substitute.For<ILogger<SystemAdminQueryActor>>();
        var actor = _fixture.CreateQueryActor(logger);

        var query = CreateQuery();

        var payload = ActorExtensions.DataSerializer.Serialize(query);
        var subject = query.Subject.ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<IQueryActorContext>();

        // Act
        actor.InvokeParseMessage(context, natsMsg);

        // Assert
        context.Received(1).SetMessageInfo(
            Arg.Is<ActorThreadId>(tid => tid == query.Subject.ThreadId),
            Arg.Is(GetDatabaseNamesQuery.Verb),
            Arg.Any<ActorMessageInfo>());
    }

    [Fact]
    public void ParseMessage_DoesNotModifyOriginalMessage()
    {
        // Arrange
        var logger = Substitute.For<ILogger<SystemAdminQueryActor>>();
        var actor = _fixture.CreateQueryActor(logger);

        var query = CreateQuery();
        var payload = ActorExtensions.DataSerializer.Serialize(query);
        var originalSubject = query.Subject.ToString();
        var originalDataLength = payload.Length;
        var natsMsg = new NatsMsg<byte[]>(originalSubject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<IQueryActorContext>();

        // Act
        actor.InvokeParseMessage(context, natsMsg);

        // Assert
        natsMsg.Subject.Should().Be(originalSubject);
        natsMsg.Data!.Length.Should().Be(originalDataLength);
    }

    #endregion

    #region ParseMessage Edge Case Tests

    [Fact]
    public void ParseMessage_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var logger = Substitute.For<ILogger<SystemAdminQueryActor>>();
        var actor = _fixture.CreateQueryActor(logger);

        var query = CreateQuery();
        var payload = ActorExtensions.DataSerializer.Serialize(query);
        var subject = query.Subject.ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        // Act
        Action act = () => actor.InvokeParseMessage(null!, natsMsg);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ParseMessage_ThrowsInvalidOperationException_WhenActorTypeIsNotQuery()
    {
        // Arrange
        var logger = Substitute.For<ILogger<SystemAdminQueryActor>>();
        var actor = _fixture.CreateQueryActor(logger);

        var query = CreateQuery();
        var payload = ActorExtensions.DataSerializer.Serialize(query);

        var invalidSubject = $"Command.{SystemAdminQueryActor.ActorName}.{GetDatabaseNamesQuery.Verb}.{Guid.NewGuid()}";
        var natsMsg = new NatsMsg<byte[]>(invalidSubject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<IQueryActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {SystemAdminQueryActor.ActorName} query from message: {invalidSubject}");
    }

    [Fact]
    public void ParseMessage_ThrowsInvalidOperationException_WhenActorNameDoesNotMatch()
    {
        // Arrange
        var logger = Substitute.For<ILogger<SystemAdminQueryActor>>();
        var actor = _fixture.CreateQueryActor(logger);

        var query = CreateQuery();
        var payload = ActorExtensions.DataSerializer.Serialize(query);

        var invalidSubject = $"Query.DifferentActor.{GetDatabaseNamesQuery.Verb}.{Guid.NewGuid()}";
        var natsMsg = new NatsMsg<byte[]>(invalidSubject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<IQueryActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {SystemAdminQueryActor.ActorName} query from message: {invalidSubject}");
    }

    [Fact]
    public void ParseMessage_ThrowsInvalidOperationException_WhenVerbIsNotRecognized()
    {
        // Arrange
        var logger = Substitute.For<ILogger<SystemAdminQueryActor>>();
        var actor = _fixture.CreateQueryActor(logger);

        var query = CreateQuery();
        var payload = ActorExtensions.DataSerializer.Serialize(query);

        var invalidSubject = $"Query.{SystemAdminQueryActor.ActorName}.UnknownVerb.{Guid.NewGuid()}";
        var natsMsg = new NatsMsg<byte[]>(invalidSubject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<IQueryActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {SystemAdminQueryActor.ActorName} query from message: {invalidSubject}");
    }

    [Fact]
    public void ParseMessage_ThrowsException_WhenPayloadIsCorrupted()
    {
        // Arrange
        var logger = Substitute.For<ILogger<SystemAdminQueryActor>>();
        var actor = _fixture.CreateQueryActor(logger);

        var query = CreateQuery();
        var corruptedPayload = new byte[] { 0x00, 0x01, 0x02, 0xFF, 0xFE };
        var subject = query.Subject.ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, corruptedPayload, default!, NatsMsgFlags.None);

        var context = Substitute.For<IQueryActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void ParseMessage_ThrowsException_WhenPayloadIsEmpty()
    {
        // Arrange
        var logger = Substitute.For<ILogger<SystemAdminQueryActor>>();
        var actor = _fixture.CreateQueryActor(logger);

        var query = CreateQuery();
        var emptyPayload = Array.Empty<byte>();
        var subject = query.Subject.ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, emptyPayload, default!, NatsMsgFlags.None);

        var context = Substitute.For<IQueryActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<Exception>();
    }

    #endregion

    #region ReceiveAsync Happy Path Tests

    [Fact]
    public async Task ReceiveAsync_GetDatabaseNamesQuery_DispatchesCorrectly()
    {
        // Arrange
        var logger = Substitute.For<ILogger<SystemAdminQueryActor>>();
        var actor = _fixture.CreateQueryActor(logger);

        var query = CreateQuery();
        var context = Substitute.For<IQueryActorContext>();

        // Act
        await actor.InvokeReceiveAsync(context, query);

        // Assert
        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetDatabaseNamesQuery.Verb,
            Arg.Is<ServiceOk<DatabaseNamesReadModel>>(r => r.Success && r.Value!.Names.Length == 7));
    }

    [Fact]
    public async Task ReceiveAsync_GetDatabaseNamesQuery_CallsReplyAsyncWithCorrectThreadIdAndVerb()
    {
        // Arrange
        var logger = Substitute.For<ILogger<SystemAdminQueryActor>>();
        var actor = _fixture.CreateQueryActor(logger);

        var query = CreateQuery();
        var context = Substitute.For<IQueryActorContext>();

        // Act
        await actor.InvokeReceiveAsync(context, query);

        // Assert
        await context.Received(1).ReplyAsync(
            Arg.Is<ActorThreadId>(tid => tid == query.Subject.ThreadId),
            Arg.Is(GetDatabaseNamesQuery.Verb),
            Arg.Any<ServiceOk<DatabaseNamesReadModel>>());
    }

    #endregion

    #region ReceiveAsync Edge Case Tests

    [Fact]
    public async Task ReceiveAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var actor = _fixture.CreateQueryActor();

        var query = CreateQuery();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(null!, query);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReceiveAsync_ThrowsArgumentNullException_WhenQueryIsNull()
    {
        // Arrange
        var actor = _fixture.CreateQueryActor();
        var context = Substitute.For<IQueryActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(context, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReceiveAsync_ThrowsInvalidOperationException_WhenQueryTypeIsNotSupported()
    {
        // Arrange
        var actor = _fixture.CreateQueryActor();
        var unsupportedQuery = Substitute.For<IQuery>();
        unsupportedQuery.Subject.Returns(new ActorSubject(ActorType.Query, SystemAdminQueryActor.ActorName, "UnknownVerb", "thread-id"));
        var context = Substitute.For<IQueryActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(context, unsupportedQuery);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to process {SystemAdminQueryActor.ActorName} query:*");
    }

    #endregion

    #region OnExceptionAsync Happy Path Tests

    [Fact]
    public async Task OnExceptionAsync_GetDatabaseNamesQuery_RepliesWithServiceResultError()
    {
        // Arrange
        var logger = Substitute.For<ILogger<SystemAdminQueryActor>>();
        var actor = _fixture.CreateQueryActor(logger);

        var query = CreateQuery();
        var threadId = query.Subject.ThreadId;
        var exception = new Exception("Database connection failed");

        var context = Substitute.For<IQueryActorContext>();

        // Act
        await actor.InvokeOnExceptionAsync(context, threadId, query, GetDatabaseNamesQuery.Verb, exception);

        // Assert
        await context.Received(1).ReplyAsync(
            threadId,
            GetDatabaseNamesQuery.Verb,
            Arg.Is<ServiceResult<DatabaseNamesReadModel>>(r =>
                !r.Success &&
                r.ErrorCode == GetDatabaseNamesQuery.ErrorId &&
                r.ErrorMessage == "Database connection failed"));
    }

    [Fact]
    public async Task OnExceptionAsync_GetDatabaseNamesQuery_PreservesErrorCode()
    {
        // Arrange
        var logger = Substitute.For<ILogger<SystemAdminQueryActor>>();
        var actor = _fixture.CreateQueryActor(logger);

        var query = CreateQuery();
        var threadId = query.Subject.ThreadId;
        var exception = new InvalidOperationException("Unexpected error occurred");

        var context = Substitute.For<IQueryActorContext>();

        // Act
        await actor.InvokeOnExceptionAsync(context, threadId, query, GetDatabaseNamesQuery.Verb, exception);

        // Assert
        await context.Received(1).ReplyAsync(
            threadId,
            GetDatabaseNamesQuery.Verb,
            Arg.Is<ServiceResult<DatabaseNamesReadModel>>(r =>
                !r.Success &&
                r.ErrorCode == GetDatabaseNamesQuery.ErrorId &&
                r.ErrorMessage == "Unexpected error occurred"));
    }

    [Fact]
    public async Task OnExceptionAsync_GetDatabaseNamesQuery_WithTimeoutException()
    {
        // Arrange
        var logger = Substitute.For<ILogger<SystemAdminQueryActor>>();
        var actor = _fixture.CreateQueryActor(logger);

        var query = CreateQuery();
        var threadId = query.Subject.ThreadId;
        var exception = new TimeoutException("Database operation timed out");

        var context = Substitute.For<IQueryActorContext>();

        // Act
        await actor.InvokeOnExceptionAsync(context, threadId, query, GetDatabaseNamesQuery.Verb, exception);

        // Assert
        await context.Received(1).ReplyAsync(
            threadId,
            GetDatabaseNamesQuery.Verb,
            Arg.Is<ServiceResult<DatabaseNamesReadModel>>(r =>
                !r.Success &&
                r.ErrorMessage == "Database operation timed out"));
    }

    [Fact]
    public async Task OnExceptionAsync_UnknownQueryType_RepliesWithServiceFailedDefault()
    {
        // Arrange
        var logger = Substitute.For<ILogger<SystemAdminQueryActor>>();
        var actor = _fixture.CreateQueryActor(logger);

        var threadId = new ActorThreadId(ActorType.Query, SystemAdminQueryActor.ActorName, "test-thread");

        var unknownQuery = Substitute.For<IQuery>();
        unknownQuery.Subject.Returns(new ActorSubject(ActorType.Query, SystemAdminQueryActor.ActorName, "UnknownVerb", "thread-id"));
        unknownQuery.ErrorCode.Returns(9999);

        var exception = new Exception("Unknown query type");
        var context = Substitute.For<IQueryActorContext>();

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

    #endregion

    #region OnExceptionAsync Edge Case Tests

    [Fact]
    public async Task OnExceptionAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var logger = Substitute.For<ILogger<SystemAdminQueryActor>>();
        var actor = _fixture.CreateQueryActor(logger);

        var query = CreateQuery();
        var threadId = query.Subject.ThreadId;
        var exception = new Exception("Test error");

        // Act — IsArgumentNull.Check(context) throws before the try/catch block
        Func<Task> act = async () => await actor.InvokeOnExceptionAsync(null!, threadId, query, GetDatabaseNamesQuery.Verb, exception);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnExceptionAsync_ThrowsArgumentNullException_WhenQueryIsNull()
    {
        // Arrange
        var logger = Substitute.For<ILogger<SystemAdminQueryActor>>();
        var actor = _fixture.CreateQueryActor(logger);

        var threadId = new ActorThreadId(ActorType.Query, SystemAdminQueryActor.ActorName, "test-thread");
        var exception = new Exception("Test error");
        var context = Substitute.For<IQueryActorContext>();

        // Act — IsArgumentNull.Check(query) throws before the try/catch block
        Func<Task> act = async () => await actor.InvokeOnExceptionAsync(context, threadId, null!, GetDatabaseNamesQuery.Verb, exception);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnExceptionAsync_ThrowsArgumentNullException_WhenVerbIsNull()
    {
        // Arrange
        var logger = Substitute.For<ILogger<SystemAdminQueryActor>>();
        var actor = _fixture.CreateQueryActor(logger);

        var query = CreateQuery();
        var threadId = query.Subject.ThreadId;
        var exception = new Exception("Test error");
        var context = Substitute.For<IQueryActorContext>();

        // Act — IsArgumentNull.Check(verb) uses string overload checking IsNullOrWhiteSpace, throws before try/catch
        Func<Task> act = async () => await actor.InvokeOnExceptionAsync(context, threadId, query, null!, exception);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnExceptionAsync_SendAsyncFails_LogsError()
    {
        // Arrange
        var logger = Substitute.For<ILogger<SystemAdminQueryActor>>();
        var actor = _fixture.CreateQueryActor(logger);

        var query = CreateQuery();
        var threadId = query.Subject.ThreadId;
        var exception = new InvalidOperationException("Original error");

        var context = Substitute.For<IQueryActorContext>();

        // Simulate ReplyAsync failure
        context.ReplyAsync(
            Arg.Any<ActorThreadId>(),
            Arg.Any<string>(),
            Arg.Any<ServiceResult<DatabaseNamesReadModel>>())
            .Returns(x => throw new Exception("ReplyAsync failed"));

        // Act
        await actor.InvokeOnExceptionAsync(context, threadId, query, GetDatabaseNamesQuery.Verb, exception);

        // Assert — should log the inner exception
        logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task OnExceptionAsync_ThrowsArgumentNullException_WhenExceptionIsNull()
    {
        // Arrange
        var logger = Substitute.For<ILogger<SystemAdminQueryActor>>();
        var actor = _fixture.CreateQueryActor(logger);

        var query = CreateQuery();
        var threadId = query.Subject.ThreadId;
        var context = Substitute.For<IQueryActorContext>();

        // Act — IsArgumentNull.Check(ex?.Message!) evaluates to null for null exception, throws before try/catch
        Func<Task> act = async () => await actor.InvokeOnExceptionAsync(context, threadId, query, GetDatabaseNamesQuery.Verb, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion
}
