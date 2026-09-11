using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.Actor;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Command.EventProjector;
public sealed class ParameterAssignmentEventProjector:ConventionalEventProjector<ParameterAssignmentCommandActor>
{
 readonly IParameterAssignmentCommandContext context;
 readonly EventProjectionDescriptor[] descriptors;
 public ParameterAssignmentEventProjector(ICommandActorContext<ParameterAssignmentCommandActor> value)
  :base(((IParameterAssignmentCommandContext)value).DurableReplayQueue,((IParameterAssignmentCommandContext)value).DbEventSource,
    ((IParameterAssignmentCommandContext)value).BlackboardService,((IParameterAssignmentCommandContext)value).Logger)
 {
  context=(IParameterAssignmentCommandContext)value;
  descriptors=[Describe<ParameterAssignmentChangedEvent>()];
 }
 public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors=>descriptors;
 public override IReadOnlyCollection<Type> ProjectedEventTypes=>descriptors.Select(x=>x.SourceEventType).ToArray();
 EventProjectionDescriptor Describe<T>() where T:class,IEvent<ParameterAssignmentEntityId> => new(typeof(T),EventProjectionIdempotencyStrategy.NaturalKeyMutation,
  async (fact,token)=>{await context.ConfigurationDb.ProjectParameterAssignmentAsync((ParameterAssignmentChangedEvent)fact);return new EventProjectionApplyResult(EventProjectionApplyOutcome.Applied);},_=>null,(_,_)=>null,false,false,false,false);
}
