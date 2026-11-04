using TomasAI.IFM.Domain.Reference.Shared.ProjectionOutcomes;
using TomasAI.IFM.Domain.Reference.ParameterSets.Persistence;
using TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.State;
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
    public ParameterSetEventProjector(ICommandActorContext<ParameterSetCommandActor> value,
        EventProjectorReliabilityOptions? reliabilityOptions = null)
     : base(((IParameterSetCommandContext)(value ?? throw new ArgumentNullException(nameof(value)))).DurableReplayQueue, ((IParameterSetCommandContext)value).DbEventSource,
       ((IParameterSetCommandContext)value).BlackboardService, ((IParameterSetCommandContext)value).Logger, reliabilityOptions)
    {
        ArgumentNullException.ThrowIfNull(value);
        context = (IParameterSetCommandContext)value;
        descriptors = [Describe<ParameterSetCreatedEvent, ParameterSetCreatedCompleteEvent, ParameterSetCreatedFailEvent>(), Describe<ParameterSetDraftSavedEvent, ParameterSetDraftSavedCompleteEvent, ParameterSetDraftSavedFailEvent>(), Describe<ParameterSetRenamedEvent, ParameterSetRenamedCompleteEvent, ParameterSetRenamedFailEvent>(), Describe<ParameterSetVersionPublishedEvent, ParameterSetVersionPublishedCompleteEvent, ParameterSetVersionPublishedFailEvent>(), Describe<ParameterSetVersionRetiredEvent, ParameterSetVersionRetiredCompleteEvent, ParameterSetVersionRetiredFailEvent>()];
    }
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => descriptors;
    EventProjectionDescriptor Describe<T, TComplete, TFail>()
        where T : class, IEvent<ParameterSetEntityId>
        where TComplete : ReferenceProjectionComplete<ParameterSetEntityId>, new()
        where TFail : ReferenceProjectionFail<ParameterSetEntityId>, new() => new EventProjectionDescriptor(typeof(T), EventProjectionIdempotencyStrategy.NaturalKeyMutation,
        async (fact, execution) =>
        {
            await RebuildAsync(fact, execution.CancellationToken).ConfigureAwait(false);
            return new EventProjectionApplyResult(EventProjectionApplyOutcome.Applied);
        },
        source => ReferenceProjectionOutcomeFactory.Complete<ParameterSetEntityId, TComplete>((T)source, "ParameterSetEvent"),
        (source, error) => ReferenceProjectionOutcomeFactory.Fail<ParameterSetEntityId, TFail>((T)source, "ParameterSetEvent", error),
        publishProcessingEvent: false, useDurableReplay: true)
        .WithReadModelRebuild((fact, token) => RebuildAsync(fact, token));

    // Materialization only: records accepted configuration and its derived cache-parameter read model. No provider subscriptions.
    async Task ProjectAsync(IParameterSetFact parameterFact, CancellationToken token)
    {
        await (context.ConfigurationDb.ProjectParameterSetAsync(parameterFact, token)).ConfigureAwait(false);
        if (parameterFact is not (ParameterSetVersionPublishedEvent or ParameterSetVersionRetiredEvent)) {
return;}
        var version = parameterFact.ReadVersion();
        if (!string.Equals(version.Reference.ComponentCode,ParameterSchemaRegistry.StrategyOptionChainCacheComponent,StringComparison.Ordinal)) {
return;}
        var parameters = version.Values().Get<StrategyOptionChainParameterSet>();
        if (parameters.ParameterSetId != version.Reference.SetId || parameters.Version != version.Reference.Version)
{
throw new InvalidDataException("Option-chain parameter event identity differs from its payload.");}
        await context.OptionChainParameters.ProjectAsync(parameters, parameterFact.Revision,
            version.Status == ParameterVersionStatus.Published, token).ConfigureAwait(false);
    }
    bool IsPostgres => string.Equals(context.ConfigurationDb.ProviderName, "System.Data.Postgres", StringComparison.Ordinal);

    Task RebuildAsync(IEvent fact, CancellationToken token)
    {
        if (!IsPostgres) {
return ProjectAsync((IParameterSetFact)fact, token);}
        var databases = context.Container.Resolve<IDbContextFactory>();
        return context.Container.Resolve<IPostgresEventTransaction>().ExecuteOnceAsync(async (transaction, cancellationToken) =>
        {
            await ParameterAcceptedProjection.ApplyAsync(databases, transaction, fact, cancellationToken).ConfigureAwait(false);
            if (fact.EventId > 0)
                await context.ConfigurationDb.CompleteParameterProjectionAsync(transaction, fact.EventId, cancellationToken).ConfigureAwait(false);
            return true;
        }, token);
    }
}
