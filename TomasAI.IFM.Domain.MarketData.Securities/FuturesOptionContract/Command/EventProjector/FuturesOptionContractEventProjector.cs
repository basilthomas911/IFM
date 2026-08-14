using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Model;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.EventProjector;

public sealed class FuturesOptionContractEventProjector(
    IDbContextFactory dbFactory,
    IActorService actorService,
    IDurableReplayQueue durableReplayQueue,
    IEventSourceActorDbContext dbEventSource,
    IBlackboardService blackboardService,
    ILogger<FuturesOptionContractEventProjector> logger,
    EventProjectorReliabilityOptions? reliabilityOptions = null)
    : ConventionalEventProjector<FuturesOptionContractCommandActor>(
        durableReplayQueue, dbEventSource, blackboardService, logger, reliabilityOptions)
{
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors =
    [
        Describe<FuturesOptionContractAddedEvent, FuturesOptionContractAddedCompleteEvent, FuturesOptionContractAddedFailEvent, FuturesOptionContractEntityId>(
            e => dbFactory.InsertFuturesOptionContractAsync(e.Contract, actorService)),
        Describe<FuturesOptionContractsAddedEvent, FuturesOptionContractsAddedCompleteEvent, FuturesOptionContractsAddedFailEvent, FuturesOptionContractsEntityId>(
            e => dbFactory.InsertFuturesOptionContractsAsync(e.Contracts, actorService)),
        Describe<FuturesOptionContractChangedEvent, FuturesOptionContractChangedCompleteEvent, FuturesOptionContractChangedFailEvent, FuturesOptionContractEntityId>(
            e => dbFactory.UpdateFuturesOptionContractAsync(e.OriginalContractId, e.Contract, actorService)),
        Describe<FuturesOptionContractRemovedEvent, FuturesOptionContractRemovedCompleteEvent, FuturesOptionContractRemovedFailEvent, FuturesOptionContractEntityId>(
            e => dbFactory.DeleteFuturesOptionContractAsync(e.ContractId))
    ];

    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes =>
        _descriptors.Select(static descriptor => descriptor.SourceEventType).ToArray();
}
