using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Catalog.Command.EventProjector;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Run.Command.EventProjector;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Catalog.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Events.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ScheduledTaskDb;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.EventProjector;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
namespace TomasAI.IFM.Domain.SystemAdmin.UnitTests;

public sealed class ScheduledTaskReadModelRebuildTests
{
    [Fact]
    public async Task Manual_host_publication_retries_in_the_durable_apply_stage_after_atomic_projection()
    {
        var writer = Substitute.For<IScheduledTaskProjectionWriter, IScheduledTaskTransactionalProjectionWriter>();
        ((IScheduledTaskTransactionalProjectionWriter)writer).ProviderName.Returns("System.Data.Postgres");
        var context = Substitute.For<ICommandActorContext>();
        var notifications = new List<ExecuteScheduledTaskRuntimeRunEvent>();
        context.SendAsync<ExecuteScheduledTaskRuntimeRunEvent, ScheduledTaskId>(
            Arg.Any<ExecuteScheduledTaskRuntimeRunEvent>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                notifications.Add(call.ArgAt<ExecuteScheduledTaskRuntimeRunEvent>(0));
                return notifications.Count == 1
                    ? ValueTask.FromException(new IOException("Host broker unavailable."))
                    : ValueTask.CompletedTask;
            });
        await using var projector = new ScheduledTaskRunEventProjector(writer, Substitute.For<IDurableReplayQueue>(),
            Substitute.For<IEventSourceActorDbContext>(), Substitute.For<IBlackboardService>(),
            NullLogger<ScheduledTaskRunEventProjector>.Instance);
        await projector.StartAsync(context);
        var descriptor = Assert.Single(projector.ProjectionDescriptors, item => item.SourceEventType == typeof(ScheduledTaskRunRequestedEvent));
        Assert.True(descriptor.UseDurableReplay);
        var run = new ScheduledTaskRun { Id = new(Guid.NewGuid()), Manual = true, Status = ScheduledTaskRunStatus.Requested,
            Environment = "Development", HostId = "host-b", OperationCommandId = Guid.NewGuid() };
        var source = new ScheduledTaskRunRequestedEvent { EventId = 9, CommandId = Guid.NewGuid(), EntityId = run.Id,
            OperationCommandId = run.OperationCommandId, ScheduledTaskRun = run };
        const string projectorName = nameof(ScheduledTaskRunEventProjector);
        var attempt = new ProjectionExecutionContext(projectorName, source.EventId, 7,
            new(projectorName, source.EventId, EventProjectorEffectKind.TargetProjection), Guid.NewGuid(),
            descriptor.IdempotencyStrategy, CancellationToken.None);

        await Assert.ThrowsAsync<IOException>(() => descriptor.ApplyAsync(source, attempt).AsTask());
        var retried = new ProjectionExecutionContext(projectorName, source.EventId, 7, attempt.EffectIdentity,
            Guid.NewGuid(), descriptor.IdempotencyStrategy, CancellationToken.None);
        Assert.True((await descriptor.ApplyAsync(source, retried)).Success);

        Assert.Equal(2, notifications.Count);
        Assert.Equal(notifications[0].Id, notifications[1].Id);
        Assert.Equal(source.CommandId, notifications[1].CommandId);
        Assert.Equal(source.OperationCommandId, notifications[1].OperationCommandId);
        Assert.Equal(new ActorSubject(ActorType.Event, ScheduledTaskIdentities.RuntimeActor(run.Environment, run.HostId),
            ExecuteScheduledTaskRuntimeRunEvent.Verb, run.Id.Format()), notifications[1].Subject);
        Assert.IsType<ScheduledTaskRunRequestedCompleteEvent>(descriptor.CompletedEventFactory(source));
        Assert.DoesNotContain(writer.ReceivedCalls(), call => call.GetMethodInfo().Name == nameof(IScheduledTaskProjectionWriter.ProjectAsync));

        var automatic = source with { ScheduledTaskRun = run with { Manual = false } };
        Assert.True((await descriptor.ApplyAsync(automatic, retried)).Success);
        await descriptor.RebuildReadModelAsync!(source, CancellationToken.None);
        Assert.Equal(2, notifications.Count);
        await writer.Received(1).ProjectAsync(run, CancellationToken.None);
    }

    [Fact]
    public async Task Every_scheduled_task_source_materializes_even_with_atomic_Postgres_writer_without_scheduler_actions()
    {
        var writer=Substitute.For<IScheduledTaskProjectionWriter,IScheduledTaskTransactionalProjectionWriter>();
        ((IScheduledTaskTransactionalProjectionWriter)writer).ProviderName.Returns("System.Data.Postgres");
        var queue=Substitute.For<IDurableReplayQueue>();var events=Substitute.For<IEventSourceActorDbContext>();
        var blackboard=Substitute.For<IBlackboardService>();
        var descriptors=new List<EventProjectionDescriptor>();
        descriptors.AddRange(new ScheduledTaskEventProjector(writer,queue,events,blackboard,NullLogger<ScheduledTaskEventProjector>.Instance).ProjectionDescriptors);
        descriptors.AddRange(new ScheduledTaskCatalogEventProjector(writer,queue,events,blackboard,NullLogger<ScheduledTaskCatalogEventProjector>.Instance).ProjectionDescriptors);
        descriptors.AddRange(new ScheduledTaskRunEventProjector(writer,queue,events,blackboard,NullLogger<ScheduledTaskRunEventProjector>.Instance).ProjectionDescriptors);
        using var cancellation=new CancellationTokenSource();
        foreach(var descriptor in descriptors)
        {
            var source=(IEvent)Activator.CreateInstance(descriptor.SourceEventType)!;
            var property=descriptor.SourceEventType.GetProperties().Single(p=>p.PropertyType==typeof(ScheduledTaskDefinition)
                ||p.PropertyType==typeof(ScheduledTaskCatalog)||p.PropertyType==typeof(ScheduledTaskRun));
            var snapshot=Activator.CreateInstance(property.PropertyType)!;property.SetValue(source,snapshot);
            writer.ClearReceivedCalls();
            Assert.NotNull(descriptor.RebuildReadModelAsync);
            await descriptor.RebuildReadModelAsync!(source,cancellation.Token);
            var call=Assert.Single(writer.ReceivedCalls());
            Assert.Equal("ProjectAsync",call.GetMethodInfo().Name);
            Assert.Same(snapshot,call.GetArguments()[0]);Assert.Equal(cancellation.Token,call.GetArguments()[1]);
        }
        Assert.Empty(queue.ReceivedCalls());Assert.Empty(events.ReceivedCalls());Assert.Empty(blackboard.ReceivedCalls());
    }
}
