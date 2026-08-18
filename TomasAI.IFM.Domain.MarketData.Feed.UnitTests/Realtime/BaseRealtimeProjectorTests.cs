using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.EventProjector.Realtime;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesMarketPrice.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.Realtime;

public sealed class BaseRealtimeProjectorTests
{
    [Fact]
    public async Task Success_PublishesSource_AppliesOnce_ThenPublishesComplete()
    {
        var steps = new List<string>();
        var context = CreateContext(ActorType.Realtime);
        context
            .When(instance => instance.SendAsync<
                FuturesTickTradeDataInsertedEvent,
                TickDataEntityId>(Arg.Any<FuturesTickTradeDataInsertedEvent>()))
            .Do(call =>
            {
                call.Arg<FuturesTickTradeDataInsertedEvent>().Subject.ActorType
                    .Should().Be(ActorType.Realtime);
                call.Arg<FuturesTickTradeDataInsertedEvent>().Subject.Name
                    .Should().Be(FuturesMarketPriceRealtimeActor.ActorName);
                steps.Add("source");
            });
        context
            .When(instance => instance.SendAsync<
                FuturesTickTradeDataInsertedCompleteEvent,
                TickDataEntityId>(Arg.Any<FuturesTickTradeDataInsertedCompleteEvent>()))
            .Do(call =>
            {
                call.Arg<FuturesTickTradeDataInsertedCompleteEvent>().Subject.ActorType
                    .Should().Be(ActorType.Realtime);
                call.Arg<FuturesTickTradeDataInsertedCompleteEvent>().Subject.Name
                    .Should().Be(FuturesMarketPriceRealtimeActor.ActorName);
                steps.Add("complete");
            });
        var projector = new TestRealtimeProjector((_, _) =>
        {
            steps.Add("apply");
            return ValueTask.CompletedTask;
        });
        await projector.StartAsync(context);

        var projected = await projector.ProcessRealtimeEventAsync(CreateSourceEvent());

        projected.Should().BeTrue();
        steps.Should().Equal("source", "apply", "complete");
        await context.Received(1).SendAsync<
            FuturesTickTradeDataInsertedEvent,
            TickDataEntityId>(Arg.Any<FuturesTickTradeDataInsertedEvent>());
        await context.Received(1).SendAsync<
            FuturesTickTradeDataInsertedCompleteEvent,
            TickDataEntityId>(Arg.Any<FuturesTickTradeDataInsertedCompleteEvent>());
        await context.DidNotReceive().SendAsync<
            FuturesTickTradeDataInsertedFailEvent,
            TickDataEntityId>(Arg.Any<FuturesTickTradeDataInsertedFailEvent>());
    }

    [Fact]
    public async Task UpdateFailure_PublishesFailOnce_AndDoesNotRetry()
    {
        var applyCalls = 0;
        var context = CreateContext(ActorType.Realtime);
        FuturesTickTradeDataInsertedFailEvent? publishedFailure = null;
        context
            .When(instance => instance.SendAsync<
                FuturesTickTradeDataInsertedFailEvent,
                TickDataEntityId>(Arg.Any<FuturesTickTradeDataInsertedFailEvent>()))
            .Do(call => publishedFailure =
                call.Arg<FuturesTickTradeDataInsertedFailEvent>());
        var projector = new TestRealtimeProjector((_, _) =>
        {
            applyCalls++;
            throw new InvalidOperationException("storage unavailable");
        });
        await projector.StartAsync(context);

        var projected = await projector.ProcessRealtimeEventAsync(CreateSourceEvent());

        projected.Should().BeFalse();
        applyCalls.Should().Be(1);
        publishedFailure.Should().NotBeNull();
        publishedFailure!.Subject.ActorType.Should().Be(ActorType.Realtime);
        publishedFailure.Subject.Name.Should().Be(FuturesMarketPriceRealtimeActor.ActorName);
        publishedFailure.ErrorMessage.Should().Be("storage unavailable");
        await context.Received(1).SendAsync<
            FuturesTickTradeDataInsertedFailEvent,
            TickDataEntityId>(Arg.Any<FuturesTickTradeDataInsertedFailEvent>());
        await context.DidNotReceive().SendAsync<
            FuturesTickTradeDataInsertedCompleteEvent,
            TickDataEntityId>(Arg.Any<FuturesTickTradeDataInsertedCompleteEvent>());
    }

    [Fact]
    public async Task Failure_DoesNotPreventTheNextRealtimeObservation()
    {
        var applyCalls = 0;
        var context = CreateContext(ActorType.Realtime);
        var projector = new TestRealtimeProjector((_, _) =>
        {
            applyCalls++;
            if (applyCalls == 1)
                throw new InvalidOperationException("transient failure");
            return ValueTask.CompletedTask;
        });
        await projector.StartAsync(context);

        var first = await projector.ProcessRealtimeEventAsync(CreateSourceEvent());
        var second = await projector.ProcessRealtimeEventAsync(CreateSourceEvent());

        first.Should().BeFalse();
        second.Should().BeTrue();
        applyCalls.Should().Be(2);
        await context.Received(2).SendAsync<
            FuturesTickTradeDataInsertedEvent,
            TickDataEntityId>(Arg.Any<FuturesTickTradeDataInsertedEvent>());
        await context.Received(1).SendAsync<
            FuturesTickTradeDataInsertedFailEvent,
            TickDataEntityId>(Arg.Any<FuturesTickTradeDataInsertedFailEvent>());
        await context.Received(1).SendAsync<
            FuturesTickTradeDataInsertedCompleteEvent,
            TickDataEntityId>(Arg.Any<FuturesTickTradeDataInsertedCompleteEvent>());
    }

    [Fact]
    public async Task Start_RejectsAReplayDurableEventActorContext()
    {
        var projector = new TestRealtimeProjector((_, _) =>
            ValueTask.CompletedTask);

        var start = async () => await projector.StartAsync(
            CreateContext(ActorType.Event));

        (await start.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Contain(nameof(ActorType.Realtime));
    }

    [Fact]
    public async Task Stop_RemovesTheRuntimeContextAndDoesNotCreateRecoveryWork()
    {
        var projector = new TestRealtimeProjector((_, _) =>
            ValueTask.CompletedTask);
        await projector.StartAsync(CreateContext(ActorType.Realtime));

        await projector.StopAsync();

        var project = async () => await projector.ProcessRealtimeEventAsync(
            CreateSourceEvent());
        await project.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void Contract_HasNoDurabilityReplayRetryOrRecoverySurface()
    {
        var memberNames = typeof(IRealtimeProjector)
            .GetMembers()
            .Select(member => member.Name)
            .ToArray();

        memberNames.Should().NotContain(name =>
            name.Contains("Durable", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Replay", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Retry", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Recovery", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Outbox", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Checkpoint", StringComparison.OrdinalIgnoreCase));
    }

    static IEventActorContext CreateContext(ActorType actorType)
    {
        var context = Substitute.For<IEventActorContext>();
        context.ActorId.Returns(new ActorMailboxId(
            actorType,
            FuturesMarketPriceRealtimeActor.ActorName));
        return context;
    }

    static FuturesTickTradeDataInsertedEvent CreateSourceEvent()
    {
        var valueDate = new DateOnly(2026, 8, 17);
        var entityId = new TickDataEntityId(
            "ESZ26",
            valueDate,
            AssetTypeId.Futures);
        return new FuturesTickTradeDataInsertedEvent
        {
            Subject = new ActorSubject(
                ActorType.Event,
                FuturesTickTradeDataInsertedEvent.Actor,
                FuturesTickTradeDataInsertedEvent.Verb,
                entityId.Format()),
            Id = Guid.NewGuid(),
            EntityId = entityId,
            EventId = 42,
            CommandId = Guid.NewGuid(),
            AggregateId = entityId.Format(),
            EventSource = nameof(BaseRealtimeProjectorTests),
            ReceivedOn = new DateTime(2026, 8, 17, 15, 30, 0, DateTimeKind.Utc),
            TickDataId = new TickDataId(
                entityId.ContractId,
                valueDate,
                42,
                new DateTime(2026, 8, 17, 15, 30, 0, DateTimeKind.Utc)),
            AssetTypeId = AssetTypeId.Futures
        };
    }

    sealed class TestRealtimeProjector
        : BaseRealtimeProjector<FuturesMarketPriceRealtimeActor>
    {
        readonly ImmutableArray<RealtimeProjectionDescriptor> _descriptors;

        public TestRealtimeProjector(
            Func<FuturesTickTradeDataInsertedEvent, CancellationToken, ValueTask> applyAsync)
            : base(NullLogger<TestRealtimeProjector>.Instance)
        {
            _descriptors =
            [
                Describe<
                    FuturesTickTradeDataInsertedEvent,
                    FuturesTickTradeDataInsertedCompleteEvent,
                    FuturesTickTradeDataInsertedFailEvent,
                    TickDataEntityId>(applyAsync)
            ];
        }

        public override string ActorName =>
            FuturesMarketPriceRealtimeActor.ActorName;
        public override string ProjectorName => nameof(TestRealtimeProjector);
        public override IReadOnlyCollection<Type> ProjectedEventTypes =>
            _descriptors.Select(descriptor => descriptor.SourceEventType).ToArray();
        public override IReadOnlyCollection<RealtimeProjectionDescriptor>
            ProjectionDescriptors => _descriptors;
    }
}
