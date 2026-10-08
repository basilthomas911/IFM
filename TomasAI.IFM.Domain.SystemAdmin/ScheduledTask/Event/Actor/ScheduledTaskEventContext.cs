using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Event.Actor;
/// <summary>Supplies the durable actor transport for runtime installation requests.</summary>
public sealed class ScheduledTaskEventContext : EventActorContext, IEventActorContext<ScheduledTaskEventActor>
{
    private readonly IActorSupervisor _supervisor;
    /// <summary>Initializes readonly supervision, transport and logging services.</summary>
    public ScheduledTaskEventContext(IActorSupervisor supervisor, ILogger<ScheduledTaskEventActor> logger, TomasAI.IFM.Domain.MarketData.Shared.ICompletedFuturesEndOfDayProjection? operationalDate = null)
        : base(supervisor, new(ActorType.Event, ScheduledTaskEventActor.Actor))
    { _supervisor = supervisor; Logger = logger; OperationalDate = operationalDate; }
    /// <summary>Gets the projection of successful session EOD completion onto the operational value date.</summary>
    public TomasAI.IFM.Domain.MarketData.Shared.ICompletedFuturesEndOfDayProjection? OperationalDate { get; }
    /// <summary>Gets the existing durable actor-event producer.</summary>
    public IJSActorProducer Producer => _supervisor.GetJSProducer(ActorId);
    /// <summary>Gets structured event actor logging.</summary>
    public ILogger<ScheduledTaskEventActor> Logger { get; }
}
