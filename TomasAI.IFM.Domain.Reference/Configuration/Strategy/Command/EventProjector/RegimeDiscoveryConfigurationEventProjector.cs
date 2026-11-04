using TomasAI.IFM.Domain.Reference.ParameterSets.Persistence;
using TomasAI.IFM.Domain.Reference.Shared.Configuration.Strategy;
using TomasAI.IFM.Domain.Reference.Shared.ProjectionOutcomes;
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

    /// <summary>Initializes the durable configuration projector.</summary>
    public RegimeDiscoveryConfigurationEventProjector(
        ICommandActorContext<RegimeDiscoveryConfigurationCommandActor> actorContext,
        EventProjectorReliabilityOptions? reliabilityOptions = null)
        : base(Typed(actorContext).DurableReplayQueue, Typed(actorContext).DbEventSource,
            Typed(actorContext).BlackboardService, Typed(actorContext).Logger, reliabilityOptions)
    {
        context = Typed(actorContext);
        descriptors =
        [
            Describe<RegimeDiscoveryConfigurationParameterSetCreatedEvent, RegimeDiscoveryConfigurationParameterSetCreatedCompleteEvent, RegimeDiscoveryConfigurationParameterSetCreatedFailEvent>(),
            Describe<RegimeDiscoveryConfigurationParameterSetPublishedEvent, RegimeDiscoveryConfigurationParameterSetPublishedCompleteEvent, RegimeDiscoveryConfigurationParameterSetPublishedFailEvent>(),
            Describe<RegimeDiscoveryConfigurationParameterSetRetiredEvent, RegimeDiscoveryConfigurationParameterSetRetiredCompleteEvent, RegimeDiscoveryConfigurationParameterSetRetiredFailEvent>()
        ];
    }

    /// <inheritdoc />
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => descriptors;
    /// <inheritdoc />

    EventProjectionDescriptor Describe<T, TComplete, TFail>()
        where T : class, IEvent<RegimeDiscoveryParameterSetEntityId>
        where TComplete : ReferenceProjectionComplete<RegimeDiscoveryParameterSetEntityId>, new()
        where TFail : ReferenceProjectionFail<RegimeDiscoveryParameterSetEntityId>, new() => new EventProjectionDescriptor(
        typeof(T), EventProjectionIdempotencyStrategy.NaturalKeyMutation,
        (source, execution) => ApplyCommittedAsync(source, execution.CancellationToken),
        source => ReferenceProjectionOutcomeFactory.Complete<RegimeDiscoveryParameterSetEntityId, TComplete>((T)source, "RegimeDiscoveryConfigurationEvent"),
        (source, error) => ReferenceProjectionOutcomeFactory.Fail<RegimeDiscoveryParameterSetEntityId, TFail>((T)source, "RegimeDiscoveryConfigurationEvent", error),
        publishProcessingEvent: false, useDurableReplay: true)
        .WithReadModelRebuild((source, token) => RebuildAsync(source, token));

    // These operations change configuration rows only; PublishAsync does not launch a workflow.
    Task ProjectAsync(IEvent source, CancellationToken token) => source switch
    {
        RegimeDiscoveryConfigurationParameterSetCreatedEvent created => context.ConfigurationDb.InsertRegimeDiscoveryDraftAsync(
            created.ParameterSet, created.Description, created.CreatedBy, token),
        RegimeDiscoveryConfigurationParameterSetPublishedEvent published => context.ConfigurationDb.PublishAsync(StrategyParameterSetKind.RegimeDiscovery,
            published.EntityId.ParameterSetId, published.EntityId.Version, published.EffectiveFromUtc, token),
        RegimeDiscoveryConfigurationParameterSetRetiredEvent retired => context.ConfigurationDb.RetireAsync(StrategyParameterSetKind.RegimeDiscovery,
            retired.EntityId.ParameterSetId, retired.EntityId.Version, retired.RetiredAtUtc, token),
        _ => throw new InvalidDataException("Unsupported configuration source event.")
    };

    bool IsPostgres => string.Equals(context.ConfigurationDb.ProviderName, "System.Data.Postgres", StringComparison.Ordinal);

    async ValueTask<EventProjectionApplyResult> ApplyCommittedAsync(IEvent source, CancellationToken token)
    {
        if (!IsPostgres)
        {
            await ProjectAsync(source, token).ConfigureAwait(false);
            return new(EventProjectionApplyOutcome.Applied);
        }

        var applied = await context.Container.Resolve<IPostgresEventTransaction>().ExecuteOnceAsync(async (transaction, cancellationToken) =>
        {
            // Claim and apply in one transaction. A failed mutation restores the marker on rollback.
            if (!await context.ConfigurationDb.CompleteParameterProjectionAsync(transaction, source.EventId, cancellationToken).ConfigureAwait(false))
                return false;
            await RegimeDiscoveryAcceptedProjection.ApplyAsync(context.ConfigurationDb, transaction, source, cancellationToken).ConfigureAwait(false);
            return true;
        }, token).ConfigureAwait(false);
        return new(applied ? EventProjectionApplyOutcome.Applied : EventProjectionApplyOutcome.AlreadyApplied);
    }

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
