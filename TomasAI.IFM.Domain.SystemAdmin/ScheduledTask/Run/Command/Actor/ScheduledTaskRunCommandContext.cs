using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Run.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Run.Command.Validation;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Run.Command.Actor;
/// <summary>Provides readonly persistence, projection and clock services.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class ScheduledTaskRunCommandContext : CommandActorContext, ICommandActorContext<ScheduledTaskRunCommandActor>
{
    /// <summary>Initializes the owning command actor context.</summary>
    public ScheduledTaskRunCommandContext(IActorSupervisor supervisor, IEventSourceActorStateRepository<ScheduledTaskRunCommandState> repository,
        IEventProjector<ScheduledTaskRunCommandActor> projector, ILogger<ScheduledTaskRunCommandActor> logger, TimeProvider? clock = null)
        : base(supervisor, new(ActorType.Command, ScheduledTaskRunCommandActor.Actor))
    { Repository = repository; EventProjector = projector; Logger = logger; Clock = clock ?? TimeProvider.System; }
    /// <summary>Gets source-event persistence.</summary>
    public IEventSourceActorStateRepository<ScheduledTaskRunCommandState> Repository { get; }
    /// <summary>Gets the committed-event projector.</summary>
    public IEventProjector<ScheduledTaskRunCommandActor> EventProjector { get; }
    /// <summary>Gets the actor clock.</summary>
    public TimeProvider Clock { get; }
    /// <summary>Gets structured actor logging.</summary>
    public ILogger<ScheduledTaskRunCommandActor> Logger { get; }
}
