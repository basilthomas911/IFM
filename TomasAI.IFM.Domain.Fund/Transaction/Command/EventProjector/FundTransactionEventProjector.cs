using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Transaction.Command.Actor;
using TomasAI.IFM.Domain.Fund.Transaction.Command.Extensions;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Fund.Transaction.Command.EventProjector;

/// <summary>
/// Projects Fund transaction domain events into the Fund read model.
/// </summary>
/// <param name="actorContext">The typed command context that supplies projector services.</param>
/// <param name="reliabilityOptions">Optional reliability behavior overrides.</param>
public sealed class FundTransactionEventProjector(
    ICommandActorContext<FundTransactionCommandActor> actorContext,
    EventProjectorReliabilityOptions? reliabilityOptions = null)
    : ConventionalEventProjector<FundTransactionCommandActor>(
        actorContext.DurableReplayQueue,
        actorContext.DbEventSource,
        actorContext.BlackboardService,
        actorContext.Logger,
        reliabilityOptions)
{
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors =
    [
        Describe<FundTransactionEvent, FundTransactionCreatedCompleteEvent, FundTransactionCreatedFailEvent, FundTransactionEntityId>(
            e => actorContext.DbFactory.FundDb.InsertFundTransactionAsync(e.FundTransaction)),
        Describe<FundTransactionsEvent, FundTransactionsCompleteEvent, FundTransactionsFailEvent, FundTransactionEntityId>(
            e => actorContext.DbFactory.FundDb.InsertFundTransactionsAsync(e.FundTransactions)),
        Describe<EndOfDayFundTransactionProcessedEvent, EndOfDayFundTransactionProcessedCompleteEvent, EndOfDayFundTransactionProcessedFailEvent, FundTransactionEntityId>(
            e => actorContext.DbFactory.FundDb.InsertFundTransactionAsync(e.FundTransaction))
    ];

    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes =>
        _descriptors.Select(static descriptor => descriptor.SourceEventType).ToArray();
}
