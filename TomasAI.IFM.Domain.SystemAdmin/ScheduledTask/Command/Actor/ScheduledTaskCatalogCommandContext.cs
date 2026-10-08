using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.Actor;
/// <summary>Provides readonly persistence, projection, clock and persisted catalog services.</summary>
public sealed class ScheduledTaskCatalogCommandContext : CommandActorContext, ICommandActorContext<ScheduledTaskCatalogCommandActor>
{
    /// <summary>Initializes the owning command actor context.</summary>
    public ScheduledTaskCatalogCommandContext(IActorSupervisor supervisor, IEventSourceActorStateRepository<ScheduledTaskCatalogCommandState> repository,
        IEventProjector<ScheduledTaskCatalogCommandActor> projector, IScheduledTaskReadStore readStore, ILogger<ScheduledTaskCatalogCommandActor> logger, TimeProvider? clock = null)
        : base(supervisor, new(ActorType.Command, ScheduledTaskCatalogCommandActor.Actor))
    { Repository = repository; EventProjector = projector; ReadStore = readStore; Logger = logger; Clock = clock ?? TimeProvider.System; }
    /// <summary>Gets source-event persistence.</summary>
    public IEventSourceActorStateRepository<ScheduledTaskCatalogCommandState> Repository { get; }
    /// <summary>Gets the committed-event projector.</summary>
    public IEventProjector<ScheduledTaskCatalogCommandActor> EventProjector { get; }
    /// <summary>Gets persisted ScyllaDB read models.</summary>
    public IScheduledTaskReadStore ReadStore { get; }
    /// <summary>Gets the actor clock.</summary>
    public TimeProvider Clock { get; }
    /// <summary>Gets structured actor logging.</summary>
    public ILogger<ScheduledTaskCatalogCommandActor> Logger { get; }
}
