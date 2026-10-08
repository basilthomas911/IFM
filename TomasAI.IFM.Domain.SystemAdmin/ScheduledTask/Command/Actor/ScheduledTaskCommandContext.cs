using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.Actor;
/// <summary>Provides readonly persistence, projection, clock and persisted catalog services.</summary>
public sealed class ScheduledTaskCommandContext : CommandActorContext, ICommandActorContext<ScheduledTaskCommandActor>
{
    /// <summary>Initializes the owning command actor context.</summary>
    public ScheduledTaskCommandContext(IActorSupervisor supervisor, IEventSourceActorStateRepository<ScheduledTaskCommandState> repository,
        IEventProjector<ScheduledTaskCommandActor> projector, IScheduledTaskReadStore readStore, ILogger<ScheduledTaskCommandActor> logger, TimeProvider? clock = null)
        : base(supervisor, new(ActorType.Command, ScheduledTaskCommandActor.Actor))
    { Repository = repository; EventProjector = projector; ReadStore = readStore; Logger = logger; Clock = clock ?? TimeProvider.System; }
    /// <summary>Gets source-event persistence.</summary>
    public IEventSourceActorStateRepository<ScheduledTaskCommandState> Repository { get; }
    /// <summary>Gets the committed-event projector.</summary>
    public IEventProjector<ScheduledTaskCommandActor> EventProjector { get; }
    /// <summary>Gets persisted ScyllaDB read models.</summary>
    public IScheduledTaskReadStore ReadStore { get; }
    /// <summary>Gets the actor clock.</summary>
    public TimeProvider Clock { get; }
    /// <summary>Gets structured actor logging.</summary>
    public ILogger<ScheduledTaskCommandActor> Logger { get; }
}
