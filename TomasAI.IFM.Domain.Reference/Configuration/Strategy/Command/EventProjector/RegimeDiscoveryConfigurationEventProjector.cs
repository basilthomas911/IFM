using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Reference.Configuration.Strategy.Command.State;
using System.Collections.Immutable;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Domain.Strategy.Contracts.Shared.Configuration;
using TomasAI.IFM.Domain.Reference.Configuration.Strategy.Command.Actor;
using TomasAI.IFM.Domain.Reference.Configuration.Strategy.Command.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Configuration.Strategy.Command.EventProjector;

/// <summary>Projects committed configuration lifecycle events into immutable ConfigurationDb rows.</summary>
public sealed class RegimeDiscoveryConfigurationEventProjector
    : ConventionalEventProjector<RegimeDiscoveryConfigurationCommandActor>
{
    readonly IRegimeDiscoveryConfigurationCommandContext context;
    readonly ImmutableArray<EventProjectionDescriptor> descriptors;

    /// <summary>Initializes the non-durable configuration projector.</summary>
    public RegimeDiscoveryConfigurationEventProjector(
        ICommandActorContext<RegimeDiscoveryConfigurationCommandActor> actorContext)
        : base(Typed(actorContext).DurableReplayQueue, Typed(actorContext).DbEventSource,
            Typed(actorContext).BlackboardService, Typed(actorContext).Logger)
    {
        context = Typed(actorContext);
        descriptors =
        [
            Describe<RegimeDiscoveryParameterSetCreatedEvent>(),
            Describe<RegimeDiscoveryParameterSetPublishedEvent>(),
            Describe<RegimeDiscoveryParameterSetRetiredEvent>()
        ];
    }

    /// <inheritdoc />
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => descriptors;
    /// <inheritdoc />

    EventProjectionDescriptor Describe<T>() where T : class, IEvent => new EventProjectionDescriptor(
        typeof(T), EventProjectionIdempotencyStrategy.NaturalKeyMutation,
        async (source, execution) => { if (!IsPostgres) {
await ProjectAsync(source, execution.CancellationToken).ConfigureAwait(false);}
return new(EventProjectionApplyOutcome.Applied); },
        _ => null, (_, _) => null, false, false, false)
        .WithReadModelRebuild((source, token) => RebuildAsync(source, token));

    // These operations change configuration rows only; PublishAsync does not launch a workflow.
    Task ProjectAsync(IEvent source, CancellationToken token) => source switch
    {
        RegimeDiscoveryParameterSetCreatedEvent created => context.ConfigurationDb.InsertRegimeDiscoveryDraftAsync(
            created.ParameterSet, created.Description, created.CreatedBy, token),
        RegimeDiscoveryParameterSetPublishedEvent published => context.ConfigurationDb.PublishAsync(StrategyParameterSetKind.RegimeDiscovery,
            published.EntityId.ParameterSetId, published.EntityId.Version, published.EffectiveFromUtc, token),
        RegimeDiscoveryParameterSetRetiredEvent retired => context.ConfigurationDb.RetireAsync(StrategyParameterSetKind.RegimeDiscovery,
            retired.EntityId.ParameterSetId, retired.EntityId.Version, retired.RetiredAtUtc, token),
        _ => throw new InvalidDataException("Unsupported configuration source event.")
    };

    bool IsPostgres => string.Equals(context.ConfigurationDb.ProviderName, "System.Data.Postgres", StringComparison.Ordinal);

    Task RebuildAsync(IEvent source, CancellationToken token)
    {
        if (!IsPostgres) {
return ProjectAsync(source, token);}
        return context.Container.Resolve<IPostgresEventTransaction>().ExecuteOnceAsync(async (transaction, cancellationToken) =>
        {
            await RegimeDiscoveryAcceptedProjection.ApplyAsync(context.ConfigurationDb, transaction, source, cancellationToken).ConfigureAwait(false);
            return true;
        }, token);
    }

    static IRegimeDiscoveryConfigurationCommandContext Typed(
        ICommandActorContext<RegimeDiscoveryConfigurationCommandActor> value)
        => value as IRegimeDiscoveryConfigurationCommandContext
           ?? throw new ArgumentException("A typed configuration context is required.", nameof(value));
}
