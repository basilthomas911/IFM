using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.Actor;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Command.EventProjector;
public sealed class ParameterStartupEventProjector:ConventionalEventProjector<ParameterStartupCommandActor>
{
 readonly IParameterStartupCommandContext context;
 readonly EventProjectionDescriptor[] descriptors;
 public ParameterStartupEventProjector(ICommandActorContext<ParameterStartupCommandActor> value)
  :base(((IParameterStartupCommandContext)value).DurableReplayQueue,((IParameterStartupCommandContext)value).DbEventSource,
    ((IParameterStartupCommandContext)value).BlackboardService,((IParameterStartupCommandContext)value).Logger)
 {
  context=(IParameterStartupCommandContext)value;
  descriptors=[Describe<ParameterStartupChangedEvent>()];
 }
 public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors=>descriptors;
 public override IReadOnlyCollection<Type> ProjectedEventTypes=>descriptors.Select(x=>x.SourceEventType).ToArray();
 EventProjectionDescriptor Describe<T>() where T:class,IEvent<ParameterStartupEntityId> => new(typeof(T),EventProjectionIdempotencyStrategy.NaturalKeyMutation,
  async (fact,token)=>{await context.ConfigurationDb.ProjectParameterStartupAsync((ParameterStartupChangedEvent)fact,token.CancellationToken);return new EventProjectionApplyResult(EventProjectionApplyOutcome.Applied);},_=>null,(_,_)=>null,false,false,false,false);
}
