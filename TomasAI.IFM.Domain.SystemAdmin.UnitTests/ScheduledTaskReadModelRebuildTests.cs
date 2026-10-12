using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ScheduledTaskDb;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.EventProjector;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
namespace TomasAI.IFM.Domain.SystemAdmin.UnitTests;

public sealed class ScheduledTaskReadModelRebuildTests
{
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
