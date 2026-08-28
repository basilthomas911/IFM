using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime;

/// <summary>Qualifies RD-19C/D recovery, post-commit ordering, and expected-version persistence.</summary>
public sealed class IntrinsicTimeStrategyWorkflowStateRepositoryTests
{
    static readonly DateTime Now = new(2026, 8, 27, 15, 0, 0, DateTimeKind.Utc);
    static readonly IntrinsicTimeStrategyWorkflowEntityId EntityId =
        IntrinsicTimeStrategyWorkflowEntityId.Create(new FuturesItiSignalEntityId(
            "ES-202612", new DateOnly(2026, 8, 27), TimeFrameType.Daily));

    [Fact]
    public async Task Load_uses_only_the_latest_authoritative_snapshot_without_projection()
    {
        var first = StartedSnapshot(revisionSeed: 0) with { EventId = 41 };
        var second = first with
        {
            EventId = 42,
            WorkflowRevision = 2,
            State = first.State with
            {
                WorkflowRevision = 2,
                UpdatedAtUtc = Now.AddSeconds(1)
            }
        };
        var fixture = RepositoryFixture.WithStream([
            Stream(first, 1),
            Stream(second, 2)
        ]);

        var state = await fixture.Repository.LoadStateAsync(Command());

        state.CurrentView.Should().BeEquivalentTo(second.State);
        state.PersistedStreamVersion.Should().Be(2);
        await fixture.Projector.DidNotReceiveWithAnyArgs()
            .DomainEventsProjectionAsync(default!);
    }

    [Fact]
    public async Task Non_empty_legacy_only_stream_fails_closed()
    {
        var fixture = RepositoryFixture.WithStream([
            new EventStreamReadModel
            {
                EventVersion = 7,
                StreamVersion = 1,
                EventTypeName = typeof(IntrinsicTimeStrategyWorkflowStartedEvent).AssemblyQualifiedName!,
                EventData = "{}"
            }
        ]);

        var load = async () => await fixture.Repository.LoadStateAsync(Command());

        var error = await load.Should().ThrowAsync<LegacyWorkflowStreamException>();
        error.Which.EventCount.Should().Be(1);
        await fixture.Projector.DidNotReceiveWithAnyArgs()
            .DomainEventsProjectionAsync(default!);
    }

    [Fact]
    public async Task Save_projects_only_after_the_expected_version_batch_commits()
    {
        var order = new List<string>();
        var fixture = RepositoryFixture.WithStream([]);
        var command = Command();
        var state = new IntrinsicTimeStrategyWorkflowCommandState();
        state.Apply(StartedSnapshot(), addEvent: true).Should().BeTrue();
        fixture.EventSource.SaveEventsAsync(
                command.StreamId,
                command.CommandId,
                state.Events,
                0,
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                order.Add("commit");
                return Task.FromResult(call.ArgAt<DomainEventCollection>(2));
            });
        fixture.Projector.DomainEventsProjectionAsync(Arg.Any<DomainEventCollection>())
            .Returns(_ =>
            {
                order.Add("project");
                return ValueTask.CompletedTask;
            });

        await fixture.Repository.SaveStateAsync(
            Substitute.For<ICommandActorContext>(), state, command, CancellationToken.None);

        order.Should().Equal("commit", "project");
        await fixture.EventSource.Received(1).SaveEventsAsync(
            command.StreamId,
            command.CommandId,
            state.Events,
            0,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Concurrency_failure_never_projects_or_dispatches_the_uncommitted_snapshot()
    {
        var fixture = RepositoryFixture.WithStream([]);
        var command = Command();
        var state = new IntrinsicTimeStrategyWorkflowCommandState();
        state.Apply(StartedSnapshot(), addEvent: true).Should().BeTrue();
        fixture.EventSource.SaveEventsAsync(
                command.StreamId,
                command.CommandId,
                state.Events,
                0,
                Arg.Any<CancellationToken>())
            .Returns<Task<DomainEventCollection>>(_ => throw new ConcurrencyException("stale"));

        var save = async () => await fixture.Repository.SaveStateAsync(
            Substitute.For<ICommandActorContext>(), state, command, CancellationToken.None);

        await save.Should().ThrowAsync<ConcurrencyException>();
        await fixture.Projector.DidNotReceiveWithAnyArgs()
            .DomainEventsProjectionAsync(default!);
    }

    static WorkflowStrategyStateUpdatedEvent StartedSnapshot(long revisionSeed = 0)
    {
        var state = new IntrinsicTimeStrategyWorkflowCommandState();
        if (revisionSeed > 0)
            throw new ArgumentOutOfRangeException(nameof(revisionSeed));
        IntrinsicTimeStrategyWorkflowCommandActor.HandleStart(
            state,
            Command(),
            new FixedTimeProvider(Now),
            TimeSpan.FromMinutes(2));
        return state.Events.Cast<WorkflowStrategyStateUpdatedEvent>().Single();
    }

    static StartIntrinsicTimeStrategyWorkflowCommand Command()
    {
        var triggerId = Guid.Parse("0198E212-3C00-7000-8000-000000000401");
        return new StartIntrinsicTimeStrategyWorkflowCommand
        {
            CommandId = triggerId,
            Subject = new ActorSubject(
                ActorType.Command,
                StartIntrinsicTimeStrategyWorkflowCommand.Actor,
                StartIntrinsicTimeStrategyWorkflowCommand.Verb,
                EntityId.Format()),
            EntityId = EntityId,
            ProposedWorkflowId = new StrategyWorkflowId(
                Guid.Parse("0198E212-3C00-7000-8000-000000000402")),
            TriggerEventId = triggerId,
            TriggerEvent = new FuturesItiSignalGeneratedEvent { EntityId = EntityId.ItiSignalEntityId },
            CorrelationId = Guid.Parse("0198E212-3C00-7000-8000-000000000403"),
            CausationId = triggerId,
            RequestedAtUtc = Now,
            WorkflowDefinitionVersion = 1,
            RegimeDiscoveryParameterPayloadSha256 = new string('a', 64)
        };
    }

    static EventStreamReadModel Stream(WorkflowStrategyStateUpdatedEvent snapshot, long streamVersion)
        => new()
        {
            EventVersion = snapshot.EventId,
            StreamVersion = streamVersion,
            EventTypeName = typeof(WorkflowStrategyStateUpdatedEvent).AssemblyQualifiedName!,
            EventData = snapshot.ToEventData()
        };

    sealed record RepositoryFixture(
        IntrinsicTimeStrategyWorkflowStateRepository Repository,
        IEventSourceActorDbContext EventSource,
        IEventProjector<IntrinsicTimeStrategyWorkflowCommandActor> Projector)
    {
        public static RepositoryFixture WithStream(ICollection<EventStreamReadModel> events)
        {
            var factory = Substitute.For<IEventSourceActorStateFactory>();
            factory.CreateState<IntrinsicTimeStrategyWorkflowCommandState>()
                .Returns(_ => new IntrinsicTimeStrategyWorkflowCommandState());
            var eventSource = Substitute.For<IEventSourceActorDbContext>();
            eventSource.GetEventStreamIdFromDbAsync(Arg.Any<string>())
                .Returns(new EventStreamIdReadModel(91, "workflow-stream"));
            eventSource.LoadActorEventStreamAsync<IntrinsicTimeStrategyWorkflowCommandState,
                    WorkflowStrategyStateUpdatedEvent>(91)
                .Returns(events);
            var projector = Substitute.For<IEventProjector<IntrinsicTimeStrategyWorkflowCommandActor>>();
            var repository = new IntrinsicTimeStrategyWorkflowStateRepository(
                factory,
                eventSource,
                Substitute.For<IActorService>(),
                projector,
                Substitute.For<ILogger<IntrinsicTimeStrategyWorkflowStateRepository>>());
            return new RepositoryFixture(repository, eventSource, projector);
        }
    }

    sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        readonly DateTimeOffset _utcNow = new(utcNow);
        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
