using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
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

namespace TomasAI.IFM.Application.Storage.IntegrationTests.ScheduledTaskDb;

[Collection("PostgreSQL plan atomicity")]
[Trait("Category", "SelfContained")]
public sealed class ScheduledTaskSnapshotRepositoryTests(TradePlanDb.PostgresTradePlanFixture fixture)
{
    [Fact]
    public async Task Definition_repository_restores_creation_and_later_changes_after_restart()
    {
        var id = new ScheduledTaskId(Guid.NewGuid());
        var command = new EnableScheduledTaskCommand { CommandId = Guid.NewGuid(), EntityId = id,
            Subject = new(ActorType.Command, EnableScheduledTaskCommand.Actor, EnableScheduledTaskCommand.Verb, id.Format()) };
        var initial = new ScheduledTaskDefinition { Id = id, Revision = 1, DesiredRevision = 1 };
        var enabled = initial with { Revision = 2, DesiredRevision = 2, Enabled = true };
        await Append(command, new ScheduledTaskCreatedEvent { EntityId = id, ScheduledTaskDefinition = initial },
            new ScheduledTaskEnabledEvent { EntityId = id, ScheduledTaskDefinition = enabled });

        await using var source = Source();
        var factory = Substitute.For<IEventSourceActorStateFactory>();
        factory.CreateState<ScheduledTaskCommandState>().Returns(_ => new ScheduledTaskCommandState());
        var repository = new ScheduledTaskStateRepository(factory, source, Substitute.For<IActorService>(),
            Substitute.For<IEventProjector<ScheduledTaskCommandActor>>(), NullLogger<ScheduledTaskStateRepository>.Instance);
        var restored = await repository.LoadStateAsync(command);
        Assert.Equivalent(enabled, restored.ScheduledTaskDefinition);
        Assert.Equal(2, restored.CommittedStreamVersion);
        Assert.Empty(restored.Events); Assert.False(restored.Updated);
    }

    [Fact]
    public async Task Run_repository_restores_requested_snapshot_and_terminal_business_outcome()
    {
        var id = new ScheduledTaskId(Guid.NewGuid());
        var command = new CompleteScheduledTaskRunCommand { CommandId = Guid.NewGuid(), EntityId = id,
            Subject = new(ActorType.Command, CompleteScheduledTaskRunCommand.Actor, CompleteScheduledTaskRunCommand.Verb, id.Format()) };
        var initial = new ScheduledTaskRun { Id = id, ScheduleId = new(Guid.NewGuid()), Revision = 1 };
        var admitted = initial with { Revision = 2, Status = ScheduledTaskRunStatus.Admitted };
        var started = admitted with { Revision = 3, Status = ScheduledTaskRunStatus.Running, ProcessId = 1234 };
        var completed = started with { Revision = 4, Status = ScheduledTaskRunStatus.Succeeded, ExitCode = 0,
            FinishedAtUtc = DateTimeOffset.UtcNow, CompletedEndOfDayValueDate = new(2026, 11, 2), StandardOutputTail = "confirmed" };
        await Append(command, new ScheduledTaskRunRequestedEvent { EntityId = id, ScheduledTaskRun = initial },
            new ScheduledTaskRunAdmissionRecordedEvent { EntityId = id, ScheduledTaskRun = admitted },
            new ScheduledTaskRunStartedEvent { EntityId = id, ScheduledTaskRun = started },
            new ScheduledTaskRunCompletedEvent { EntityId = id, ScheduledTaskRun = completed });

        await using var source = Source();
        var factory = Substitute.For<IEventSourceActorStateFactory>();
        factory.CreateState<ScheduledTaskRunCommandState>().Returns(_ => new ScheduledTaskRunCommandState());
        var repository = new ScheduledTaskRunStateRepository(factory, source, Substitute.For<IActorService>(),
            Substitute.For<IEventProjector<ScheduledTaskRunCommandActor>>(), NullLogger<ScheduledTaskRunStateRepository>.Instance);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var restored = await repository.LoadStateAsync(command, deadline.Token);
        Assert.Equivalent(completed, restored.ScheduledTaskRun);
        Assert.Equal(4, restored.CommittedStreamVersion);
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
        Assert.Equal(EventWriteIdentityRegistry.Default.GetIdentity(typeof(ScheduledTaskCatalogProjectRegisteredEvent)).EventTypeName, rows[0].EventTypeName);
        var factory = Substitute.For<IEventSourceActorStateFactory>();
        factory.CreateState<ScheduledTaskCatalogCommandState>().Returns(_ => new ScheduledTaskCatalogCommandState());
        var repository = new ScheduledTaskCatalogStateRepository(factory, source, Substitute.For<IActorService>(),
            Substitute.For<IEventProjector<ScheduledTaskCatalogCommandActor>>(), NullLogger<ScheduledTaskCatalogStateRepository>.Instance);
        var restored = await repository.LoadStateAsync(command);
        Assert.Equivalent(ready, restored.ScheduledTaskCatalog);
        Assert.Equal(4, restored.CommittedStreamVersion);
        Assert.Empty(restored.Events); Assert.False(restored.Updated);
    }

    private async Task Append(ICommand command, params IEvent[] events) => await fixture.Transactions.ExecuteOnceAsync(async (transaction, token) =>
    {
        for (var index = 0; index < events.Length; index++)
            await transaction.AppendAsync(command.StreamId, Guid.NewGuid(), events[index], index, token);
        return true;
    });

    private EventSourceActorDbContext Source()
    {
        EventSourceActorDbContext source = null!;
        var factory = new DbContextFactory(new DbContextResolver(_ => source));
        var cache = Substitute.For<IRedisCache>();
        return source = new(fixture.Settings, factory, new BlackboardService(cache, new SystemTextJsonSerializer()), NullLogger<DbProvider>.Instance);
    }
}
