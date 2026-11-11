using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Catalog.Command.Actor;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Catalog.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.Actor;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Run.Command.Actor;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Run.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Catalog.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Catalog.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Catalog.Events.Domain;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Events.Domain;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Events.Domain;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using TomasAI.IFM.Shared.Storage;
using Xunit;
using Xunit.Abstractions;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.ScheduledTaskDb;

[Collection("PostgreSQL plan atomicity")]
[Trait("Category", "SelfContained")]
public sealed class ScheduledTaskSnapshotRepositoryTests(TradePlanDb.PostgresTradePlanFixture fixture, ITestOutputHelper output)
{
    [Fact]
    public async Task Definition_repository_restores_creation_and_later_changes_after_restart()
    {
        var id = new ScheduledTaskId(Guid.NewGuid());
        var command = new EnableScheduledTaskCommand { CommandId = Guid.NewGuid(), EntityId = id,
            Subject = new(ActorType.Command, EnableScheduledTaskCommand.Actor, EnableScheduledTaskCommand.Verb, id.Format()) };
        var initial = new ScheduledTaskDefinition { Id = id, Revision = 1, DesiredRevision = 1 };
        const int head = 512;
        var enabled = initial with
        {
            Revision = head,
            DesiredRevision = head,
            Enabled = true,
            AppliedEnabled = true,
            AppliedRevision = head - 1,
            InstallationFingerprint = "fixture-installation",
            ReviewedArtifactDigest = "sha256:fixture",
            ActiveRunId = Guid.NewGuid(),
            LastAdmittedFireUtc = DateTimeOffset.UnixEpoch.AddHours(1),
            Schedule = new() { TaskKey = "fixture-task", TimeZoneId = "UTC" }
        };
        IEvent[] history =
        [
            new ScheduledTaskCreatedEvent { EntityId = id, ScheduledTaskDefinition = initial },
            .. Enumerable.Range(2, head - 2).Select(revision => new ScheduledTaskScheduleChangedEvent
            {
                EntityId = id,
                ScheduledTaskDefinition = initial with { Revision = revision, DesiredRevision = revision }
            }),
            new ScheduledTaskEnabledEvent { EntityId = id, ScheduledTaskDefinition = enabled }
        ];
        await Append(command, history);

        await using var source = Source();
        var factory = Substitute.For<IEventSourceActorStateFactory>();
        factory.CreateState<ScheduledTaskCommandState>().Returns(_ => new ScheduledTaskCommandState());
        var repository = new ScheduledTaskStateRepository(factory, source, Substitute.For<IActorService>(),
            Substitute.For<IEventProjector<ScheduledTaskCommandActor>>(), NullLogger<ScheduledTaskStateRepository>.Instance);
        var baseline = await CompareReadWindows(source, command, () => new ScheduledTaskCommandState(),
            [typeof(ScheduledTaskCreatedEvent), typeof(ScheduledTaskScheduleChangedEvent), typeof(ScheduledTaskEnabledEvent)], head, () => repository.LoadStateAsync(command));
        var restored = await repository.LoadStateAsync(command);
        Assert.Equivalent(enabled, restored.ScheduledTaskDefinition);
        Assert.Equivalent(baseline.ScheduledTaskDefinition, restored.ScheduledTaskDefinition);
        Assert.Equal(head, restored.CommittedStreamVersion);
        Assert.Empty(restored.Events); Assert.False(restored.Updated);

        var disabled = enabled with { Revision = head + 1, DesiredRevision = head + 1, Enabled = false };
        await fixture.Transactions.ExecuteOnceAsync(async (transaction, token) =>
        {
            await transaction.AppendAsync(new EnlistedEventAppend(command.StreamId, Guid.NewGuid(),
                new ScheduledTaskDisabledEvent { EntityId = id, ScheduledTaskDefinition = disabled },
                restored.CommittedStreamVersion), token);
            return true;
        });
        var reloaded = await repository.LoadStateAsync(command);
        Assert.Equivalent(disabled, reloaded.ScheduledTaskDefinition);
        Assert.Equal(head + 1, reloaded.CommittedStreamVersion);
    }

    [Theory]
    [InlineData(ScheduledTaskRunStatus.Succeeded)]
    [InlineData(ScheduledTaskRunStatus.Failed)]
    [InlineData(ScheduledTaskRunStatus.Uncertain)]
    public async Task Run_repository_restores_requested_snapshot_and_terminal_business_outcome(ScheduledTaskRunStatus outcome)
    {
        var id = new ScheduledTaskId(Guid.NewGuid());
        var command = new CompleteScheduledTaskRunCommand { CommandId = Guid.NewGuid(), EntityId = id,
            Subject = new(ActorType.Command, CompleteScheduledTaskRunCommand.Actor, CompleteScheduledTaskRunCommand.Verb, id.Format()) };
        var initial = new ScheduledTaskRun { Id = id, ScheduleId = new(Guid.NewGuid()), Revision = 1 };
        var admitted = initial with { Revision = 2, Status = ScheduledTaskRunStatus.Admitted };
        var started = admitted with { Revision = 3, Status = ScheduledTaskRunStatus.Running, ProcessId = 1234 };
        var staged = started with { Revision = 4, Stage = "FeedsStopped", ValueDate = new DateOnly(2026, 11, 2) };
        var completed = staged with
        {
            Revision = 5,
            Status = outcome,
            ExitCode = outcome == ScheduledTaskRunStatus.Uncertain ? null : outcome == ScheduledTaskRunStatus.Succeeded ? 0 : 1,
            FinishedAtUtc = DateTimeOffset.UtcNow,
            CompletedEndOfDayValueDate = outcome == ScheduledTaskRunStatus.Succeeded ? new DateOnly(2026, 11, 2) : null,
            StandardOutputTail = $"Outcome: {outcome}"
        };
        IEvent terminalEvent = outcome switch
        {
            ScheduledTaskRunStatus.Succeeded => new ScheduledTaskRunCompletedEvent { EntityId = id, ScheduledTaskRun = completed },
            ScheduledTaskRunStatus.Failed => new ScheduledTaskRunFailedEvent { EntityId = id, ScheduledTaskRun = completed },
            ScheduledTaskRunStatus.Uncertain => new ScheduledTaskRunUncertainRecordedEvent { EntityId = id, ScheduledTaskRun = completed },
            _ => throw new ArgumentOutOfRangeException(nameof(outcome))
        };
        await Append(command, new ScheduledTaskRunRequestedEvent { EntityId = id, ScheduledTaskRun = initial },
            new ScheduledTaskRunAdmissionRecordedEvent { EntityId = id, ScheduledTaskRun = admitted },
            new ScheduledTaskRunStartedEvent { EntityId = id, ScheduledTaskRun = started },
            new ScheduledTaskRunStageRecordedEvent { EntityId = id, ScheduledTaskRun = staged });

        await using var source = Source();
        var factory = Substitute.For<IEventSourceActorStateFactory>();
        factory.CreateState<ScheduledTaskRunCommandState>().Returns(_ => new ScheduledTaskRunCommandState());
        var repository = new ScheduledTaskRunStateRepository(factory, source, Substitute.For<IActorService>(),
            Substitute.For<IEventProjector<ScheduledTaskRunCommandActor>>(), NullLogger<ScheduledTaskRunStateRepository>.Instance);
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var beforeOutcome = await repository.LoadStateAsync(command, deadline.Token);
        Assert.Equivalent(staged, beforeOutcome.ScheduledTaskRun);
        Assert.Equal(4, beforeOutcome.CommittedStreamVersion);
        await fixture.Transactions.ExecuteOnceAsync(async (transaction, token) =>
        {
            await transaction.AppendAsync(new EnlistedEventAppend(command.StreamId, Guid.NewGuid(), terminalEvent, beforeOutcome.CommittedStreamVersion), token);
            return true;
        }, deadline.Token);

        var baseline = await CompareReadWindows(source, command, () => new ScheduledTaskRunCommandState(), [typeof(ScheduledTaskRunRequestedEvent),
            typeof(ScheduledTaskRunStageRecordedEvent), terminalEvent.GetType()], 5, () => repository.LoadStateAsync(command, deadline.Token));
        var restored = await repository.LoadStateAsync(command, deadline.Token);
        Assert.Equivalent(completed, restored.ScheduledTaskRun);
        Assert.Equivalent(baseline.ScheduledTaskRun, restored.ScheduledTaskRun);
        Assert.Equal(5, restored.CommittedStreamVersion);
        Assert.Empty(restored.Events); Assert.False(restored.Updated);
    }

    [Fact]
    public async Task Catalog_repository_restores_first_registration_and_tail_using_frozen_historical_identity()
    {
        var id = new ScheduledTaskId(Guid.NewGuid());
        var command = new RegisterScheduledTaskProjectCommand { CommandId = Guid.NewGuid(), EntityId = id,
            Subject = new(ActorType.Command, RegisterScheduledTaskProjectCommand.Actor, RegisterScheduledTaskProjectCommand.Verb, id.Format()) };
        var initial = new ScheduledTaskCatalog { Id = id, Revision = 1, Projects = [new() { TaskKey = "first" }] };
        var observed = initial with { Revision = 2, HostCapability = new() { Ready = false, Generation = 1 } };
        var registration = observed with { Revision = 3, Projects = [.. initial.Projects, new() { TaskKey = "second" }] };
        var ready = registration with { Revision = 4, HostCapability = new() { Ready = true, Generation = 2 } };
        await Append(command, new ScheduledTaskCatalogProjectRegisteredEvent { EntityId = id, ScheduledTaskCatalog = initial },
            new ScheduledTaskCatalogHostCapabilityRecordedEvent { EntityId = id, ScheduledTaskCatalog = observed },
            new ScheduledTaskCatalogProjectRegisteredEvent { EntityId = id, ScheduledTaskCatalog = registration },
            new ScheduledTaskCatalogHostCapabilityRecordedEvent { EntityId = id, ScheduledTaskCatalog = ready });

        await using var source = Source();
        var streamId = await source.GetEventStreamIdAsync(command.StreamId, CancellationToken.None);
        var rows = new List<EventStreamReadModel>();
        await source.MapReduceActorEventStreamFromCreationAsync<ScheduledTaskCatalogCommandState>(streamId,
            page => rows.AddRange(page.Select(row => new EventStreamReadModel { EventVersion = row.EventVersion,
                EventTypeName = row.EventTypeName, EventData = row.EventData, StreamVersion = row.StreamVersion })), CancellationToken.None);
        Assert.Equal(4, rows.Count);
        var registrationWindow = new List<long>();
        await source.MapReduceActorEventStreamFromCreationWithCheckpointsAsync<ScheduledTaskCatalogCommandState>(
            streamId, [typeof(ScheduledTaskCatalogProjectRegisteredEvent)],
            page => registrationWindow.AddRange(page.Select(row => row.StreamVersion)));
        Assert.Equal(new long[] { 1, 3, 4 }, registrationWindow);
        Assert.Equal(EventWriteIdentityRegistry.Default.GetIdentity(typeof(ScheduledTaskCatalogProjectRegisteredEvent)).EventTypeName, rows[0].EventTypeName);
        var factory = Substitute.For<IEventSourceActorStateFactory>();
        factory.CreateState<ScheduledTaskCatalogCommandState>().Returns(_ => new ScheduledTaskCatalogCommandState());
        var repository = new ScheduledTaskCatalogStateRepository(factory, source, Substitute.For<IActorService>(),
            Substitute.For<IEventProjector<ScheduledTaskCatalogCommandActor>>(), NullLogger<ScheduledTaskCatalogStateRepository>.Instance);
        var baseline = await CompareReadWindows(source, command, () => new ScheduledTaskCatalogCommandState(),
            [typeof(ScheduledTaskCatalogProjectRegisteredEvent), typeof(ScheduledTaskCatalogHostCapabilityRecordedEvent)], 4, () => repository.LoadStateAsync(command));
        var restored = await repository.LoadStateAsync(command);
        Assert.Equivalent(ready, restored.ScheduledTaskCatalog);
        Assert.Equivalent(baseline.ScheduledTaskCatalog, restored.ScheduledTaskCatalog);
        Assert.Equal(4, restored.CommittedStreamVersion);
        Assert.Empty(restored.Events); Assert.False(restored.Updated);

        var next = ready with { Revision = 5, Projects = [.. ready.Projects, new() { TaskKey = "third" }] };
        await fixture.Transactions.ExecuteOnceAsync(async (transaction, token) =>
        {
            await transaction.AppendAsync(new EnlistedEventAppend(command.StreamId, Guid.NewGuid(),
                new ScheduledTaskCatalogProjectRegisteredEvent { EntityId = id, ScheduledTaskCatalog = next },
                restored.CommittedStreamVersion), token);
            return true;
        });
        var reloaded = await repository.LoadStateAsync(command);
        Assert.Equivalent(next, reloaded.ScheduledTaskCatalog);
        Assert.Equal(5, reloaded.CommittedStreamVersion);
    }

    private async Task<TState> CompareReadWindows<TState>(EventSourceActorDbContext source, ICommand command,
        Func<TState> create, IReadOnlyList<Type> eligibleTypes, int head, Func<ValueTask<TState>> load)
        where TState : IEventSourceActorState<TState>
    {
        var fullCount = 0;
        long stream = 0;
        var coldFullStart = Stopwatch.GetTimestamp();
        var baseline = await FullReplay();
        var firstFull = Stopwatch.GetElapsedTime(coldFullStart).TotalMilliseconds;
        var coldCheckpointStart = Stopwatch.GetTimestamp();
        var loaded = await load();
        var firstCheckpoint = Stopwatch.GetElapsedTime(coldCheckpointStart).TotalMilliseconds;

        const int samples = 20;
        var full = new double[samples];
        var checkpoint = new double[samples];
        for (var sample = 0; sample < samples; sample++)
        {
            // Alternate which path goes first to reduce ordering and cache bias.
            if (sample % 2 == 0)
            {
                full[sample] = await MeasureFull();
                checkpoint[sample] = await MeasureCheckpoint();
            }
            else
            {
                checkpoint[sample] = await MeasureCheckpoint();
                full[sample] = await MeasureFull();
            }
        }

        var versions = new List<long>();
        await source.MapReduceActorEventStreamFromCreationWithCheckpointsAsync<TState>(
            stream, eligibleTypes, page => versions.AddRange(page.Select(row => row.StreamVersion)));
        Array.Sort(full);
        Array.Sort(checkpoint);
        output.WriteLine($"{typeof(TState).Name}: {fullCount} -> {versions.Count} rows. " +
            $"First load {firstFull:F2} -> {firstCheckpoint:F2} ms. " +
            $"Warm median {Median(full):F2} -> {Median(checkpoint):F2} ms. " +
            $"Warm p95 {full[18]:F2} -> {checkpoint[18]:F2} ms.");
        Assert.Equal(head, loaded.CommittedStreamVersion);
        Assert.Equal(head, fullCount);
        Assert.Equal(new long[] { 1, head }, versions);
        Assert.Equal(head, baseline.CommittedStreamVersion);
        return baseline;

        async Task<TState> FullReplay()
        {
            fullCount = 0;
            stream = await source.GetEventStreamIdAsync(command.StreamId, CancellationToken.None);
            var state = create();
            state.Id = command.Subject.ThreadId;
            await source.MapReduceActorEventStreamFromCreationAsync<TState>(stream, page => state.ReplayEvents(Count(page)));
            return state;
        }

        async Task<double> MeasureFull()
        {
            var start = Stopwatch.GetTimestamp();
            await FullReplay();
            return Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        }

        async Task<double> MeasureCheckpoint()
        {
            var start = Stopwatch.GetTimestamp();
            await load();
            return Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        }

        static double Median(double[] sorted) => (sorted[9] + sorted[10]) / 2;

        IEnumerable<EventStreamReadModel> Count(IEnumerable<EventStreamReadModel> page)
        {
            foreach (var row in page)
            {
                fullCount++;
                yield return row;
            }
        }
    }

    private async Task Append(ICommand command, params IEvent[] events) => await fixture.Transactions.ExecuteOnceAsync(async (transaction, token) =>
    {
        for (var index = 0; index < events.Length; index++)
            await transaction.AppendAsync(new EnlistedEventAppend(command.StreamId, Guid.NewGuid(), events[index], index), token);
        return true;
    });

    private EventSourceActorDbContext Source()
    {
        EventSourceActorDbContext source = null!;
        var factory = new DbContextFactory(new DbContextResolver(_ => source));

        return source = new(fixture.Settings, factory,  NullLogger<DbProvider>.Instance);
    }
}
