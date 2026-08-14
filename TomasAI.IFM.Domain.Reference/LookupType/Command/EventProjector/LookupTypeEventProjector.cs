using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Reference.LookupType.Command.Actor;
using TomasAI.IFM.Domain.Reference.Services;
using TomasAI.IFM.Domain.Reference.Shared;
using TomasAI.IFM.Domain.Reference.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Reference.LookupType.Command.EventProjector;

public sealed class LookupTypeEventProjector(
    IDbContextFactory dbFactory,
    IDurableReplayQueue durableReplayQueue,
    IEventSourceActorDbContext dbEventSource,
    IBlackboardService blackboardService,
    ILogger<LookupTypeEventProjector> logger,
    EventProjectorReliabilityOptions? reliabilityOptions = null)
    : ConventionalEventProjector<LookupTypeCommandActor>(
        durableReplayQueue, dbEventSource, blackboardService, logger, reliabilityOptions)
{
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors =
    [
        Describe<LookupTypeAddedEvent, LookupTypeAddedCompleteEvent, LookupTypeAddedFailEvent, LookupTypeId>(
            (Func<LookupTypeAddedEvent, Task>)(async e =>
            {
                await dbFactory.ReferenceDb.InsertLookupTypeAsync(e.LookupType).ConfigureAwait(false);
                Invalidate(blackboardService);
            })),
        Describe<LookupTypeChangedEvent, LookupTypeChangedCompleteEvent, LookupTypeChangedFailEvent, LookupTypeId>(
            (Func<LookupTypeChangedEvent, Task>)(async e =>
            {
                await dbFactory.ReferenceDb.UpdateLookupTypeAsync(e.EntityId, e.LookupType).ConfigureAwait(false);
                Invalidate(blackboardService);
            })),
        Describe<LookupTypeRemovedEvent, LookupTypeRemovedCompleteEvent, LookupTypeRemovedFailEvent, LookupTypeId>(
            (Func<LookupTypeRemovedEvent, Task>)(async e =>
            {
                await dbFactory.ReferenceDb.DeleteLookupTypeAsync(e.EntityId).ConfigureAwait(false);
                Invalidate(blackboardService);
            }))
    ];

    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes =>
        _descriptors.Select(static descriptor => descriptor.SourceEventType).ToArray();

    static void Invalidate(IBlackboardService blackboardService)
    {
        blackboardService.Reference.ReferenceLookup.Remove();
        ReferenceLookupCacheGeneration.Invalidate();
    }
}
