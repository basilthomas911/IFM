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
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.EventProjector;

public sealed class FuturesOptionContractEventProjector(
    ICommandActorContext<FuturesOptionContractCommandActor> actorContext,
    EventProjectorReliabilityOptions? reliabilityOptions = null)
    : ConventionalEventProjector<FuturesOptionContractCommandActor>(
        actorContext.DurableReplayQueue, actorContext.DbEventSource,
        actorContext.BlackboardService, actorContext.Logger, reliabilityOptions)
{
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors =
    [
        Describe<FuturesOptionContractAddedEvent, FuturesOptionContractAddedCompleteEvent, FuturesOptionContractAddedFailEvent, FuturesOptionContractEntityId>(
            e => actorContext.DbFactory.InsertFuturesOptionContractAsync(e.Contract, actorContext.ActorService)),
        Describe<FuturesOptionContractsAddedEvent, FuturesOptionContractsAddedCompleteEvent, FuturesOptionContractsAddedFailEvent, FuturesOptionContractsEntityId>(
            e => actorContext.DbFactory.InsertFuturesOptionContractsAsync(e.Contracts, actorContext.ActorService)),
        Describe<FuturesOptionContractChangedEvent, FuturesOptionContractChangedCompleteEvent, FuturesOptionContractChangedFailEvent, FuturesOptionContractEntityId>(
            e => actorContext.DbFactory.UpdateFuturesOptionContractAsync(e.OriginalContractId, e.Contract, actorContext.ActorService)),
        Describe<FuturesOptionContractRemovedEvent, FuturesOptionContractRemovedCompleteEvent, FuturesOptionContractRemovedFailEvent, FuturesOptionContractEntityId>(
            e => actorContext.DbFactory.DeleteFuturesOptionContractAsync(e.ContractId))
    ];

    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes =>
        _descriptors.Select(static descriptor => descriptor.SourceEventType).ToArray();
}
