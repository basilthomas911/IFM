using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Transaction.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Fund.Transaction.Command.EventProjector;

public sealed class FundTransactionEventProjector(
    IDbContextFactory dbFactory,
    IDurableReplayQueue durableReplayQueue,
    IEventSourceActorDbContext dbEventSource,
    IBlackboardService blackboardService,
    ILogger<FundTransactionEventProjector> logger,
    EventProjectorReliabilityOptions? reliabilityOptions = null)
    : ConventionalEventProjector<FundTransactionCommandActor>(
        durableReplayQueue, dbEventSource, blackboardService, logger, reliabilityOptions)
{
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors =
    [
        Describe<FundTransactionEvent, FundTransactionCreatedCompleteEvent, FundTransactionCreatedFailEvent, FundTransactionEntityId>(
            e => dbFactory.FundDb.InsertFundTransactionAsync(e.FundTransaction)),
        Describe<FundTransactionsEvent, FundTransactionsCompleteEvent, FundTransactionsFailEvent, FundTransactionEntityId>(
            e => dbFactory.FundDb.InsertFundTransactionsAsync(e.FundTransactions)),
        Describe<EndOfDayFundTransactionProcessedEvent, EndOfDayFundTransactionProcessedCompleteEvent, EndOfDayFundTransactionProcessedFailEvent, FundTransactionEntityId>(
            e => dbFactory.FundDb.InsertFundTransactionAsync(e.FundTransaction))
    ];

    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes =>
        _descriptors.Select(static descriptor => descriptor.SourceEventType).ToArray();
}
