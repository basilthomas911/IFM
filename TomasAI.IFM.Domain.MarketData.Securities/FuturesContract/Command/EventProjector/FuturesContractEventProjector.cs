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
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.EventProjector;

public sealed class FuturesContractEventProjector(
    ICommandActorContext<FuturesContractCommandActor> actorContext,
    EventProjectorReliabilityOptions? reliabilityOptions = null)
    : ConventionalEventProjector<FuturesContractCommandActor>(
        actorContext.DurableReplayQueue, actorContext.DbEventSource,
        actorContext.BlackboardService, actorContext.Logger, reliabilityOptions)
{
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors =
    [
        Describe<FuturesContractAddedEvent, FuturesContractAddedCompleteEvent, FuturesContractAddedFailEvent, FuturesContractId>(
            e => actorContext.DbFactory.SecuritiesDb.InsertFuturesContractAsync(e.Contract)),
        Describe<FuturesContractChangedEvent, FuturesContractChangedCompleteEvent, FuturesContractChangedFailEvent, FuturesContractId>(
            e => actorContext.DbFactory.SecuritiesDb.UpdateFuturesContractAsync(e.OriginalContractId, e.Contract)),
        Describe<FuturesContractRemovedEvent, FuturesContractRemovedCompleteEvent, FuturesContractRemovedFailEvent, FuturesContractId>(
            e => actorContext.DbFactory.SecuritiesDb.DeleteFuturesContractAsync(e.ContractId))
    ];

    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes =>
        _descriptors.Select(static descriptor => descriptor.SourceEventType).ToArray();
}
