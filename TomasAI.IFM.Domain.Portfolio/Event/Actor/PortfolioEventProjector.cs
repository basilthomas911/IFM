using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Shared.EventSourcing;
using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Command.Actor;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.FinancialPolicy.Events;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventProjector;

namespace TomasAI.IFM.Domain.Portfolio.Projection;

public sealed class PortfolioEventProjector(
    IDurableReplayQueue replayQueue, IEventSourceActorDbContext eventSource, IBlackboardService blackboard,
    ILogger<PortfolioEventProjector> logger, IPortfolioEventStore events, IPortfolioDbWriteContext projections,
    EventProjectorReliabilityOptions? options = null, IPostgresEventTransaction? transactions = null)
    : ConventionalEventProjector<PortfolioCommandActor>(replayQueue, eventSource, blackboard, logger, options)
{
    static readonly ImmutableArray<Type> Types =
    [
        typeof(PortfolioCreatedEvent), typeof(PortfolioVersionAddedEvent), typeof(PortfolioOperatingStateChangedEvent),
        typeof(FundAddedToPortfolioEvent), typeof(PortfolioRetiredEvent), typeof(FundAllocationDelegatedEvent),
        typeof(FundRiskEnvelopeDelegatedEvent),
        typeof(DraftPortfolioDeletedEvent), typeof(PortfolioFinancialPolicyAssignedEvent),
    ];
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors =
        [
            DescribeNotification<PortfolioCreatedEvent, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)).WithReadModelRebuild((source,ct) => RebuildAsync(source, ((PortfolioCreatedEvent)source).ProjectionFacts, transactions,ct)),
            DescribeNotification<PortfolioVersionAddedEvent, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)).WithReadModelRebuild((source,ct) => RebuildAsync(source, ((PortfolioVersionAddedEvent)source).ProjectionFacts, transactions,ct)),
            DescribeNotification<PortfolioOperatingStateChangedEvent, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)).WithReadModelRebuild((source,ct) => RebuildAsync(source, ((PortfolioOperatingStateChangedEvent)source).ProjectionFacts, transactions,ct)),
            DescribeNotification<FundAddedToPortfolioEvent, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)).WithReadModelRebuild((source,ct) => RebuildAsync(source, ((FundAddedToPortfolioEvent)source).ProjectionFacts, transactions,ct)),
            DescribeNotification<PortfolioRetiredEvent, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)).WithReadModelRebuild((source,ct) => RebuildAsync(source, ((PortfolioRetiredEvent)source).ProjectionFacts, transactions,ct)),
            DescribeNotification<FundAllocationDelegatedEvent, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)).WithReadModelRebuild((source,ct) => RebuildAsync(source, ((FundAllocationDelegatedEvent)source).ProjectionFacts, transactions,ct)),
            DescribeNotification<FundRiskEnvelopeDelegatedEvent, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)).WithReadModelRebuild((source,ct) => RebuildAsync(source, ((FundRiskEnvelopeDelegatedEvent)source).ProjectionFacts, transactions,ct)),
            DescribeNotification<PortfolioFinancialPolicyAssignedEvent, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)).WithReadModelRebuild((source,ct) => RebuildAsync(source, ((PortfolioFinancialPolicyAssignedEvent)source).ProjectionFacts, transactions,ct)),
            DescribeNotification<DraftPortfolioDeletedEvent, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)).WithReadModelRebuild((source,ct) => RebuildAsync(source, ((DraftPortfolioDeletedEvent)source).ProjectionFacts, transactions,ct)),
        ];

    static Task<bool> RebuildAsync(IEvent source, TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.PortfolioProjectionFacts? facts,
        IPostgresEventTransaction? owner, CancellationToken token)
        => (owner ?? throw new InvalidOperationException("Portfolio rebuild transaction is not registered.")).ExecuteOnceAsync(async (transaction,ct) =>
        {
            await PortfolioDbContextExtensions.PersistAcceptedPortfolioAsync(transaction,source,
                facts ?? throw new InvalidOperationException("Older Portfolio events require verified baseline facts."),ct).ConfigureAwait(false);
            return true;
        },token);

    public override IReadOnlyCollection<Type> ProjectedEventTypes => Types;
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;
}
