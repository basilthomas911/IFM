using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.Storage;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Reference.StrategyCatalog.Command.Actor;
using TomasAI.IFM.Domain.Reference.ParameterSets.Persistence;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.StrategyCatalog.Command.EventProjector;

public sealed class StrategyCatalogEventProjector(
    IDurableReplayQueue queue, IEventSourceActorDbContext eventSource, ILogger<StrategyCatalogCommandActor> logger, StrategyCatalogService catalog,
    EventProjectorReliabilityOptions? options = null)
    : ConventionalEventProjector<StrategyCatalogCommandActor>(queue, eventSource, logger, options)
{
    readonly IReadOnlyCollection<EventProjectionDescriptor> descriptors =
    [
        new EventProjectionDescriptor(typeof(StrategyCatalogSourceEvent), EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            async (source, execution) => new(await catalog.ProjectAsync((StrategyCatalogSourceEvent)source, false, execution.CancellationToken).ConfigureAwait(false)
                ? EventProjectionApplyOutcome.Applied : EventProjectionApplyOutcome.AlreadyApplied),
            source => Complete((StrategyCatalogSourceEvent)source),
            (source, error) => Fail((StrategyCatalogSourceEvent)source, error),
            publishProcessingEvent: false, useDurableReplay: true)
            .WithReadModelRebuild(async (source, token) =>
                _ = await catalog.ProjectAsync((StrategyCatalogSourceEvent)source, true, token).ConfigureAwait(false))
    ];

    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => descriptors;

    const string OutcomeActor = "StrategyCatalogEvent";

    static ICompleteEvent Complete(StrategyCatalogSourceEvent source) => source.Subject.Verb switch
    {
        SaveStrategyCatalogDraftCommand.Verb => ReferenceProjectionOutcomeFactory.Complete<ActorEntityId, StrategyCatalogDraftSavedCompleteEvent>(source, OutcomeActor),
        PublishStrategyCatalogVersionCommand.Verb => ReferenceProjectionOutcomeFactory.Complete<ActorEntityId, StrategyCatalogVersionPublishedCompleteEvent>(source, OutcomeActor),
        RetireStrategyCatalogVersionCommand.Verb => ReferenceProjectionOutcomeFactory.Complete<ActorEntityId, StrategyCatalogVersionRetiredCompleteEvent>(source, OutcomeActor),
        _ => throw new InvalidDataException("Unknown strategy catalog source operation.")
    };

    static IErrorEvent Fail(StrategyCatalogSourceEvent source, Exception error) => source.Subject.Verb switch
    {
        SaveStrategyCatalogDraftCommand.Verb => ReferenceProjectionOutcomeFactory.Fail<ActorEntityId, StrategyCatalogDraftSavedFailEvent>(source, OutcomeActor, error),
        PublishStrategyCatalogVersionCommand.Verb => ReferenceProjectionOutcomeFactory.Fail<ActorEntityId, StrategyCatalogVersionPublishedFailEvent>(source, OutcomeActor, error),
        RetireStrategyCatalogVersionCommand.Verb => ReferenceProjectionOutcomeFactory.Fail<ActorEntityId, StrategyCatalogVersionRetiredFailEvent>(source, OutcomeActor, error),
        _ => throw new InvalidDataException("Unknown strategy catalog source operation.")
    };
}
