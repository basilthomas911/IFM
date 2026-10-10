using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.Actor;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Command.EventProjector;

public sealed class ParameterSetEventProjector : ConventionalEventProjector<ParameterSetCommandActor>
{
    readonly IParameterSetCommandContext context;
    readonly EventProjectionDescriptor[] descriptors;
    public ParameterSetEventProjector(ICommandActorContext<ParameterSetCommandActor> value)
     : base(((IParameterSetCommandContext)value).DurableReplayQueue, ((IParameterSetCommandContext)value).DbEventSource,
       ((IParameterSetCommandContext)value).BlackboardService, ((IParameterSetCommandContext)value).Logger)
    {
        context = (IParameterSetCommandContext)value;
        descriptors = [Describe<ParameterSetCreatedEvent>(), Describe<ParameterDraftSavedEvent>(), Describe<ParameterSetRenamedEvent>(), Describe<ParameterVersionPublishedEvent>(), Describe<ParameterVersionRetiredEvent>()];
    }
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes => descriptors.Select(x => x.SourceEventType).ToArray();
    EventProjectionDescriptor Describe<T>() where T : class, IParameterSetFact => new(typeof(T), EventProjectionIdempotencyStrategy.NaturalKeyMutation,
     async (fact, execution) =>
     {
         var parameterFact = (IParameterSetFact)fact;
         await context.ConfigurationDb.ProjectParameterSetAsync(parameterFact);
         if (fact is ParameterVersionPublishedEvent or ParameterVersionRetiredEvent)
         {
             var version = System.Text.Json.JsonSerializer.Deserialize<ParameterSetVersion>(parameterFact.VersionJson)
                 ?? throw new InvalidDataException("Parameter version event payload is missing.");
             if (version.Reference.ComponentCode == ParameterSchemaRegistry.StrategyOptionChainCacheComponent)
             {
                 var parameters = TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache.StrategyOptionChainParameterSet.Read(version.PayloadJson);
                 if (parameters.ParameterSetId != version.Reference.SetId || parameters.Version != version.Reference.Version)
                     throw new InvalidDataException("Option-chain parameter event identity differs from its payload.");
                 await context.OptionChainParameters.ProjectAsync(parameters, parameterFact.Revision,
                     version.Status == ParameterVersionStatus.Published, execution.CancellationToken).ConfigureAwait(false);
             }
         }
         return new EventProjectionApplyResult(EventProjectionApplyOutcome.Applied);
     }, _ => null, (_, _) => null, false, typeof(T) == typeof(ParameterVersionPublishedEvent) || typeof(T) == typeof(ParameterVersionRetiredEvent), false, false);
}
