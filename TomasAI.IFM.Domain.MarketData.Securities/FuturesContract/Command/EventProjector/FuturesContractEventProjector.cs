using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.EventProjector;

public sealed class FuturesContractEventProjector(
    IDbContextFactory dbFactory,
    IDurableReplayQueue durableReplayQueue,
    IEventSourceActorDbContext dbEventSource,
    IBlackboardService blackboardService,
    ILogger<FuturesContractEventProjector> logger,
    EventProjectorReliabilityOptions? reliabilityOptions = null)
    : ConventionalEventProjector<FuturesContractCommandActor>(
        durableReplayQueue, dbEventSource, blackboardService, logger, reliabilityOptions)
{
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors =
    [
        Describe<FuturesContractAddedEvent, FuturesContractAddedCompleteEvent, FuturesContractAddedFailEvent, FuturesContractId>(
            e => dbFactory.SecuritiesDb.InsertFuturesContractAsync(e.Contract)),
        Describe<FuturesContractChangedEvent, FuturesContractChangedCompleteEvent, FuturesContractChangedFailEvent, FuturesContractId>(
            e => dbFactory.SecuritiesDb.UpdateFuturesContractAsync(e.OriginalContractId, e.Contract)),
        Describe<FuturesContractRemovedEvent, FuturesContractRemovedCompleteEvent, FuturesContractRemovedFailEvent, FuturesContractId>(
            e => dbFactory.SecuritiesDb.DeleteFuturesContractAsync(e.ContractId))
    ];

    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes =>
        _descriptors.Select(static descriptor => descriptor.SourceEventType).ToArray();
}
