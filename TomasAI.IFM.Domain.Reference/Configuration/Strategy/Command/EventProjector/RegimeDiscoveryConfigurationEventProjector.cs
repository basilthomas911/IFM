using System.Collections.Immutable;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
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
    public override IReadOnlyCollection<Type> ProjectedEventTypes => descriptors.Select(x => x.SourceEventType).ToArray();

    EventProjectionDescriptor Describe<T>() where T : class, IEvent => new(
        typeof(T), EventProjectionIdempotencyStrategy.NaturalKeyMutation,
        async (domainEvent, _) =>
        {
            switch (domainEvent)
            {
                case RegimeDiscoveryParameterSetCreatedEvent created:
                    await context.ConfigurationDb.InsertRegimeDiscoveryDraftAsync(
                        created.ParameterSet, created.Description, created.CreatedBy).ConfigureAwait(false);
                    break;
                case RegimeDiscoveryParameterSetPublishedEvent published:
                    await context.ConfigurationDb.PublishAsync(StrategyParameterSetKind.RegimeDiscovery,
                        published.EntityId.ParameterSetId, published.EntityId.Version,
                        published.EffectiveFromUtc).ConfigureAwait(false);
                    break;
                case RegimeDiscoveryParameterSetRetiredEvent retired:
                    await context.ConfigurationDb.RetireAsync(StrategyParameterSetKind.RegimeDiscovery,
                        retired.EntityId.ParameterSetId, retired.EntityId.Version,
                        retired.RetiredAtUtc).ConfigureAwait(false);
                    break;
            }
            return new EventProjectionApplyResult(EventProjectionApplyOutcome.Applied);
        }, _ => null, (_, _) => null, false, false, false);

    static IRegimeDiscoveryConfigurationCommandContext Typed(
        ICommandActorContext<RegimeDiscoveryConfigurationCommandActor> value)
        => value as IRegimeDiscoveryConfigurationCommandContext
           ?? throw new ArgumentException("A typed configuration context is required.", nameof(value));
}
