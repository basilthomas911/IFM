using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.Actor;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Command.EventProjector;
public sealed class ParameterSetEventProjector:ConventionalEventProjector<ParameterSetCommandActor>
{
 readonly IParameterSetCommandContext context;
 readonly EventProjectionDescriptor[] descriptors;
 public ParameterSetEventProjector(ICommandActorContext<ParameterSetCommandActor> value)
  :base(((IParameterSetCommandContext)value).DurableReplayQueue,((IParameterSetCommandContext)value).DbEventSource,
    ((IParameterSetCommandContext)value).BlackboardService,((IParameterSetCommandContext)value).Logger)
 {
  context=(IParameterSetCommandContext)value;
  descriptors=[Describe<ParameterSetCreatedEvent>(),Describe<ParameterDraftSavedEvent>(),Describe<ParameterSetRenamedEvent>(),Describe<ParameterVersionPublishedEvent>(),Describe<ParameterVersionRetiredEvent>()];
 }
 public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors=>descriptors;
 public override IReadOnlyCollection<Type> ProjectedEventTypes=>descriptors.Select(x=>x.SourceEventType).ToArray();
 EventProjectionDescriptor Describe<T>() where T:class,IParameterSetFact => new(typeof(T),EventProjectionIdempotencyStrategy.NaturalKeyMutation,
  async (fact,token)=>{await context.ConfigurationDb.ProjectParameterSetAsync((IParameterSetFact)fact);return new EventProjectionApplyResult(EventProjectionApplyOutcome.Applied);},_=>null,(_,_)=>null,false,false,false);
}
