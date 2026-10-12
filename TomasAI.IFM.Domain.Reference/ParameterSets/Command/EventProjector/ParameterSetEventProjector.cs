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
    public ParameterSetEventProjector(ICommandActorContext<ParameterSetCommandActor> value)
     : base(((IParameterSetCommandContext)(value ?? throw new ArgumentNullException(nameof(value)))).DurableReplayQueue, ((IParameterSetCommandContext)value).DbEventSource,
       ((IParameterSetCommandContext)value).BlackboardService, ((IParameterSetCommandContext)value).Logger)
    {
        ArgumentNullException.ThrowIfNull(value);
        context = (IParameterSetCommandContext)value;
        descriptors = [Describe<ParameterSetCreatedEvent>(), Describe<ParameterDraftSavedEvent>(), Describe<ParameterSetRenamedEvent>(), Describe<ParameterVersionPublishedEvent>(), Describe<ParameterVersionRetiredEvent>()];
    }
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => descriptors;
    EventProjectionDescriptor Describe<T>() where T : class, IParameterSetFact => new EventProjectionDescriptor(typeof(T), EventProjectionIdempotencyStrategy.NaturalKeyMutation,
        async (fact, execution) => { if (!IsPostgres) {
await ProjectAsync((IParameterSetFact)fact, execution.CancellationToken).ConfigureAwait(false);}
return new(EventProjectionApplyOutcome.Applied); },
        _ => null, (_, _) => null, false, typeof(T) == typeof(ParameterVersionPublishedEvent) || typeof(T) == typeof(ParameterVersionRetiredEvent), false, false)
        .WithReadModelRebuild((fact, token) => RebuildAsync(fact, token));

    // Materialization only: records accepted configuration and its derived cache-parameter read model. No provider subscriptions.
    async Task ProjectAsync(IParameterSetFact parameterFact, CancellationToken token)
    {
        await (context.ConfigurationDb.ProjectParameterSetAsync(parameterFact, token)).ConfigureAwait(false);
        if (parameterFact is not (ParameterVersionPublishedEvent or ParameterVersionRetiredEvent)) {
return;}
        var version = System.Text.Json.JsonSerializer.Deserialize<ParameterSetVersion>(parameterFact.VersionJson)
            ?? throw new InvalidDataException("Parameter version event payload is missing.");
        if (!string.Equals(version.Reference.ComponentCode,ParameterSchemaRegistry.StrategyOptionChainCacheComponent,StringComparison.Ordinal)) {
return;}
        var parameters = TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache.StrategyOptionChainParameterSet.Read(version.PayloadJson);
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
            return true;
        }, token);
    }
}
