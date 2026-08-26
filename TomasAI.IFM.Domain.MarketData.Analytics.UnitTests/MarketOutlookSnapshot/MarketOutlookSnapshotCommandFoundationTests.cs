using FluentAssertions;
using MessagePack;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.MarketOutlookSnapshot;

public sealed class MarketOutlookSnapshotCommandFoundationTests
{
    [Fact]
    public void CommandContext_ExposesReadonlyClosedGenericDependencies()
    {
        var supervisor = Substitute.For<IActorSupervisor>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        var blackboard = Substitute.For<IBlackboardService>();
        var logger = Substitute.For<ILogger<MarketOutlookSnapshotCommandActor>>();

        var context = new MarketOutlookSnapshotCommandContext(
            supervisor,
            dbFactory,
            blackboard,
            logger);

        context.ActorId.Should().Be(new ActorMailboxId(
            ActorType.Command,
            MarketOutlookSnapshotCommandActor.ActorName));
        context.DbFactory.Should().BeSameAs(dbFactory);
        context.BlackboardService.Should().BeSameAs(blackboard);
        context.Logger.Should().BeSameAs(logger);
        context.Should().BeAssignableTo<ICommandActorContext<MarketOutlookSnapshotCommandActor>>();
    }

    [Fact]
    public void Commands_MessagePackRoundTrip_PreserveIdentityAndIdempotencyMetadata()
    {
        var entityId = EntityId();
        var sourceEventId = Guid.NewGuid();
        var observed = new ObserveMarketOutlookComponentCommand(
            entityId,
            sourceEventId,
            42,
            new DateTime(2026, 8, 26, 14, 30, 0, DateTimeKind.Utc),
            nameof(FuturesRsiSignalGeneratedCompleteEvent),
            futuresRsiSignal: SampleData.AtrRsiSignals[0])
        {
            CommandId = Guid.NewGuid(),
            Subject = Subject(ObserveMarketOutlookComponentCommand.Verb, entityId)
        };

        var observedRoundTrip = MessagePackSerializer.Deserialize<ObserveMarketOutlookComponentCommand>(
            MessagePackSerializer.Serialize(observed));

        observedRoundTrip.EntityId.Should().Be(entityId);
        observedRoundTrip.SourceEventId.Should().Be(sourceEventId);
        observedRoundTrip.SourceEventSequence.Should().Be(42);
        observedRoundTrip.ComponentCount.Should().Be(1);
        observedRoundTrip.RouteTo.Should().Be(BoundedContextName.MarketOutlookSnapshotBoundedContext);

        var publish = new PublishMarketOutlookSnapshotCommand(
            entityId,
            Guid.NewGuid(),
            43,
            observed.SourceEventTimestamp.AddMinutes(1),
            SampleData.EodData)
        {
            CommandId = Guid.NewGuid(),
            Subject = Subject(PublishMarketOutlookSnapshotCommand.Verb, entityId)
        };

        var publishRoundTrip = MessagePackSerializer.Deserialize<PublishMarketOutlookSnapshotCommand>(
            MessagePackSerializer.Serialize(publish));

        publishRoundTrip.EntityId.Should().Be(entityId);
        publishRoundTrip.FuturesEodData.Should().BeEquivalentTo(SampleData.EodData);
        publishRoundTrip.RouteTo.Should().Be(BoundedContextName.MarketOutlookSnapshotBoundedContext);
    }

    [Fact]
    public void DomainEvents_ConvertToProjectionResults_WithCompleteCheckpoint()
    {
        var entityId = EntityId();
        var sourceEventId = Guid.NewGuid();
        var checkpoint = WorkingState(
            entityId,
            1,
            Watermark(MarketOutlookComponentType.Rsi, sourceEventId));
        var observed = new MarketOutlookComponentObservedEvent
        {
            Subject = EventSubject(MarketOutlookComponentObservedEvent.Verb, entityId),
            Id = Guid.NewGuid(),
            EntityId = entityId,
            CommandId = Guid.NewGuid(),
            WorkingState = checkpoint,
            SourceEventId = sourceEventId,
            SourceEventSequence = 7,
            SourceEventName = "source"
        };

        var completed = observed.ToCompleteEvent<
            MarketOutlookComponentObservedCompleteEvent,
            MarketOutlookEntityId>();
        var failed = observed.ToFailEvent<
            MarketOutlookComponentObservedFailEvent,
            MarketOutlookEntityId>(new InvalidOperationException("projection failed"));

        completed.Should().BeOfType<MarketOutlookComponentObservedCompleteEvent>()
            .Which.WorkingState.Should().BeEquivalentTo(checkpoint);
        failed.Should().BeOfType<MarketOutlookComponentObservedFailEvent>()
            .Which.ErrorMessage.Should().Be("projection failed");
    }

    [Fact]
    public void Replay_ComponentAndPublishedEvents_ReconstructsLatestImmutableCheckpoint()
    {
        var entityId = EntityId();
        var firstSource = Guid.NewGuid();
        var publishSource = Guid.NewGuid();
        var firstWatermarks = new[] { Watermark(MarketOutlookComponentType.Rsi, firstSource) };
        var observedState = WorkingState(entityId, 1, firstWatermarks);
        var publishedSnapshot = new MarketOutlookSnapshotReadModel
        {
            ContractId = entityId.ContractId,
            ValueDate = entityId.ValueDate,
            Revision = 1,
            UpdatedOn = DateTime.UtcNow,
            FuturesEodData = SampleData.EodData
        };
        var publishedState = observedState with
        {
            Revision = 2,
            Status = MarketOutlookStateStatus.Published,
            FuturesEodData = SampleData.EodData,
            PublishedSnapshot = publishedSnapshot,
            SourceWatermarks =
            [
                Watermark(MarketOutlookComponentType.Rsi, firstSource),
                Watermark(MarketOutlookComponentType.Eod, publishSource)
            ]
        };
        var state = new MarketOutlookSnapshotCommandState();

        state.ReplayEvents([
            new MarketOutlookComponentObservedEvent
            {
                EntityId = entityId,
                WorkingState = observedState,
                SourceEventId = firstSource
            },
            new MarketOutlookSnapshotPublishedEvent
            {
                EntityId = entityId,
                WorkingState = publishedState,
                MarketOutlook = publishedSnapshot,
                SourceEventId = publishSource
            }
        ]);
        firstWatermarks[0] = Watermark(MarketOutlookComponentType.Rsi, Guid.NewGuid());

        state.WorkingState.Should().BeEquivalentTo(publishedState);
        state.WorkingState.Status.Should().Be(MarketOutlookStateStatus.Published);
        state.HasProcessed(firstSource).Should().BeTrue();
        state.HasProcessed(publishSource).Should().BeTrue();
        state.WorkingState.SourceWatermarks.Should().NotContain(firstWatermarks[0]);
    }

    static MarketOutlookWorkingStateReadModel WorkingState(
        MarketOutlookEntityId entityId,
        long revision,
        params MarketOutlookSourceWatermark[] sourceWatermarks)
        => new()
        {
            EntityId = entityId,
            Revision = revision,
            UpdatedOn = DateTime.UtcNow,
            FuturesRsiSignal = SampleData.AtrRsiSignals[0],
            SourceWatermarks = sourceWatermarks,
            Status = MarketOutlookStateStatus.Collecting
        };

    static MarketOutlookSourceWatermark Watermark(
        MarketOutlookComponentType componentType,
        Guid sourceEventId)
        => new()
        {
            ComponentType = componentType,
            SourceEventId = sourceEventId,
            SourceEventSequence = 1,
            SourceEventTimestamp = DateTime.UtcNow
        };

    static MarketOutlookEntityId EntityId()
        => new(SampleData.ContractId, SampleData.ValueDate);

    static ActorSubject Subject(string verb, MarketOutlookEntityId entityId)
        => new(ActorType.Command, ObserveMarketOutlookComponentCommand.Actor, verb, entityId.Format());

    static ActorSubject EventSubject(string verb, MarketOutlookEntityId entityId)
        => new(ActorType.Event, MarketOutlookComponentObservedEvent.Actor, verb, entityId.Format());
}
