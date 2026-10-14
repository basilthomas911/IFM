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
using TomasAI.IFM.Domain.Portfolio.FinancialPolicy.Command.Actor;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.FinancialPolicy.Events;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventProjector;

namespace TomasAI.IFM.Domain.Portfolio.Projection;

public sealed class PortfolioFinancialPolicyEventProjector(
    IDurableReplayQueue replayQueue, IEventSourceActorDbContext eventSource, IBlackboardService blackboard,
    ILogger<PortfolioFinancialPolicyEventProjector> logger, IPortfolioEventStore events, IPortfolioDbWriteContext projections,
    EventProjectorReliabilityOptions? options = null, IPostgresEventTransaction? transactions = null)
    : ConventionalEventProjector<PortfolioFinancialPolicyCommandActor>(replayQueue, eventSource, blackboard, logger, options)
{
    static readonly ImmutableArray<Type> Types =
    [
        typeof(PortfolioFinancialPolicyCreatedEvent), typeof(PortfolioFinancialPolicyVersionAddedEvent),
        typeof(PortfolioFinancialPolicyActivatedEvent), typeof(PortfolioFinancialPolicyRetiredEvent),
        typeof(DraftPortfolioFinancialPolicyDeletedEvent),
    ];
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors =
    [
        DescribeNotification<PortfolioFinancialPolicyCreatedEvent, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)).WithReadModelRebuild((source,ct) => RebuildAsync(source, ((PortfolioFinancialPolicyCreatedEvent)source).ProjectionFacts, transactions,ct)),
        DescribeNotification<PortfolioFinancialPolicyVersionAddedEvent, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)).WithReadModelRebuild((source,ct) => RebuildAsync(source, ((PortfolioFinancialPolicyVersionAddedEvent)source).ProjectionFacts, transactions,ct)),
        DescribeNotification<PortfolioFinancialPolicyActivatedEvent, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)).WithReadModelRebuild((source,ct) => RebuildAsync(source, ((PortfolioFinancialPolicyActivatedEvent)source).ProjectionFacts, transactions,ct)),
        DescribeNotification<PortfolioFinancialPolicyRetiredEvent, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)).WithReadModelRebuild((source,ct) => RebuildAsync(source, ((PortfolioFinancialPolicyRetiredEvent)source).ProjectionFacts, transactions,ct)),
        DescribeNotification<DraftPortfolioFinancialPolicyDeletedEvent, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)).WithReadModelRebuild((source,ct) => RebuildAsync(source, ((DraftPortfolioFinancialPolicyDeletedEvent)source).ProjectionFacts, transactions,ct)),
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
