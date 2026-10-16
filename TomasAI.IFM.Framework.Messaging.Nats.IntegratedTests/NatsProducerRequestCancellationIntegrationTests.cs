using FluentAssertions;
using MessagePack;
using Microsoft.Extensions.Logging.Abstractions;
using NATS.Client.Core;
using NATS.Net;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Framework.Messaging.Nats.IntegratedTests;

[Trait("Category", "Integration")]
public sealed class NatsProducerRequestCancellationIntegrationTests
{
    readonly string url = Environment.GetEnvironmentVariable("IFM_NATS_URL") ?? "nats://localhost:4222";
    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1707",Justification="Keep descriptive xUnit test names consistent with the repository test convention.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA2000",Justification="The test deliberately exercises transferred resource ownership and explicit disposal assertions.")]
    public async Task InFlightRequest_IsCanceledByCallerOrProducerStop(bool callerSupplied, bool cancelCaller)
    {
        var subject = new ActorSubject(ActorType.Query, $"Cancellation{Guid.NewGuid():N}", "Read", "1");
var server = new NatsClient(url);
await using var serverDisposal = server.ConfigureAwait(true);
        await server.ConnectAsync();
var subscription = await (server.Connection.SubscribeCoreAsync<byte[]>(subject.ToString())).ConfigureAwait(true);
await using var subscriptionDisposal = subscription.ConfigureAwait(true);
        await server.Connection.PingAsync();
var manager = new NatsConnectionManager();
await using var managerDisposal = manager.ConfigureAwait(true);
        var producer = new NatsActorProducer(new NatsProducerOptions { Url = url }, NullLogger.Instance, manager);
        using var caller = new CancellationTokenSource();
        using var deadline = new CancellationTokenSource(Timeout);
        try
        {
            await producer.StartAsync(subject.ActorId, deadline.Token);
            var pending = producer.RequestAsync<TestResult, TestQuery>(subject,
                new TestQuery { Subject = subject }, callerSupplied ? caller.Token : CancellationToken.None).AsTask();
            // Ensure the request reached the broker/subscriber before exercising cancellation.
            var received = await subscription.Msgs.ReadAsync(deadline.Token);
            received.ReplyTo.Should().NotBeNullOrEmpty();
            if (cancelCaller) await caller.CancelAsync().ConfigureAwait(true); else await producer.StopAsync(deadline.Token);
            Func<Task> finish = async () => await (pending.WaitAsync(Timeout)).ConfigureAwait(true);
            await finish.Should().ThrowAsync<OperationCanceledException>();

            // The shared connection remains usable after request cancellation or producer stop.
            await server.Connection.PingAsync(deadline.Token);
        }
        finally
        {
            await producer.StopAsync();
        }
    }

    [Fact]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1707",Justification="Keep descriptive xUnit test names consistent with the repository test convention.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA2000",Justification="The test deliberately exercises transferred resource ownership and explicit disposal assertions.")]
    public async Task SuccessfulRequests_ReuseShutdownSource_AndWorkAfterProducerRestart()
    {
        var subject = new ActorSubject(ActorType.Query, $"CancellationSuccess{Guid.NewGuid():N}", "Read", "1");
var server = new NatsClient(url);
await using var serverDisposal = server.ConfigureAwait(true);
        await server.ConnectAsync();
var subscription = await (server.Connection.SubscribeCoreAsync<byte[]>(subject.ToString())).ConfigureAwait(true);
await using var subscriptionDisposal = subscription.ConfigureAwait(true);
        await server.Connection.PingAsync();
var manager = new NatsConnectionManager();
await using var managerDisposal = manager.ConfigureAwait(true);
        var producer = new NatsActorProducer(new NatsProducerOptions { Url = url }, NullLogger.Instance, manager);
        using var deadline = new CancellationTokenSource(Timeout);
        try
        {
            await producer.StartAsync(subject.ActorId, deadline.Token);
            for (var i = 0; i < 3; i++)
            {
                var pending = producer.RequestAsync<TestResult, TestQuery>(subject, new TestQuery { Subject = subject }).AsTask();
                var received = await subscription.Msgs.ReadAsync(deadline.Token);
                await received.ReplyAsync(new ServiceResult<TestResult>(new TestResult { Value = i }),
                    serializer: NatsMessagePackSerializer<ServiceResult<TestResult>>.Default, cancellationToken: deadline.Token);
                var result = await pending.WaitAsync(Timeout);
                result.Success.Should().BeTrue();
                result.Value!.Value.Should().Be(i);
                if (i == 1)
                    await producer.StopAsync(deadline.Token);
            }
        }
        finally
        {
            await producer.StopAsync();
        }
    }

    [Theory]
    [InlineData(ActorType.Command)]
    [InlineData(ActorType.Function)]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1707",Justification="Keep descriptive xUnit test names consistent with the repository test convention.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA2000",Justification="The test deliberately exercises transferred resource ownership and explicit disposal assertions.")]
    public async Task CommandAndFunctionRequests_UseTypedCompatibleReplies(ActorType actorType)
    {
        var subject = new ActorSubject(actorType, $"TypedReply{Guid.NewGuid():N}", "Run", "42");
        var entityId = new ActorEntityId("42");
        var command = new TestCommand
        {
            Subject = subject,
            EntityId = entityId,
            CommandId = Guid.NewGuid(),
            Value = 91
        };
var server = new NatsClient(url);
await using var serverDisposal = server.ConfigureAwait(true);
        await server.ConnectAsync();
var subscription = await (server.Connection.SubscribeCoreAsync<byte[]>(subject.ToString())).ConfigureAwait(true);
await using var subscriptionDisposal = subscription.ConfigureAwait(true);
        await server.Connection.PingAsync();
var manager = new NatsConnectionManager();
await using var managerDisposal = manager.ConfigureAwait(true);
        var producer = new NatsActorProducer(new NatsProducerOptions { Url = url }, NullLogger.Instance, manager);
        using var deadline = new CancellationTokenSource(Timeout);

        try
        {
            await producer.StartAsync(subject.ActorId, deadline.Token);
            var pending = actorType == ActorType.Function
                ? producer.RequestFunctionAsync<TestCommand, ActorEntityId, TestResult>(
                    subject, command, entityId, deadline.Token).AsTask()
                : producer.RequestAsync<TestCommand, ActorEntityId, TestResult>(
                    subject, command, entityId, deadline.Token).AsTask();
            var received = await subscription.Msgs.ReadAsync(deadline.Token);
            var decoded = NatsMessagePackSerializer<TestCommand>.Default.Deserialize(
                new System.Buffers.ReadOnlySequence<byte>(received.Data));
            decoded.Should().BeEquivalentTo(command);
            await received.ReplyAsync(
                new ServiceResult<TestResult>(new TestResult { Value = 92 }),
                serializer: NatsMessagePackSerializer<ServiceResult<TestResult>>.Default,
                cancellationToken: deadline.Token);

            var result = await pending.WaitAsync(Timeout);
            result.Success.Should().BeTrue();
            result.Value!.Value.Should().Be(92);
        }
        finally
        {
            await producer.StopAsync();
        }
    }

    [MessagePackObject]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1515",Justification="xUnit discovers public test classes and fixtures.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1034",Justification="These public nested types are concrete serializer test fixtures.")]
    public sealed class TestQuery : IQuery<TestResult>
    {
        [Key(0)] public ActorSubject Subject { get; init; }
        [IgnoreMember] public IActorEntityId EntityId => ActorEntityId.Default;
        [IgnoreMember] public int ErrorCode => 0;
        [IgnoreMember] public string? QueryParams => null;
    }

    [MessagePackObject]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1034",Justification="These public nested types are concrete serializer test fixtures.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1515",Justification="xUnit discovers public test classes and fixtures.")]
    public sealed class TestResult
    {
        [Key(0)] public int Value { get; init; }
    }

    [MessagePackObject]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1515",Justification="xUnit discovers public test classes and fixtures.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1034",Justification="These public nested types are concrete serializer test fixtures.")]
    public sealed class TestCommand : ICommand<ActorEntityId>
    {
        [Key(0)] public ActorSubject Subject { get; init; }
        [Key(1)] public ActorEntityId EntityId { get; init; }
        [Key(2)] public Guid CommandId { get; init; }
        [Key(3)] public int Value { get; init; }
        [IgnoreMember] public string CommandName => nameof(TestCommand);
        [IgnoreMember] public BoundedContextName RouteTo => BoundedContextName.Undefined;
        [IgnoreMember] public string StreamId => Subject.StreamId;
        [IgnoreMember] public string EventSource => "IntegrationTest";
        [IgnoreMember] public int ErrorCode => 0;
    }
}
