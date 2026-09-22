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

public sealed class PortfolioFinancialPolicyEventProjector(
    IDurableReplayQueue replayQueue, IEventSourceActorDbContext eventSource, IBlackboardService blackboard,
    ILogger<PortfolioFinancialPolicyEventProjector> logger, IPortfolioEventStore events, IPortfolioDbWriteContext projections,
    EventProjectorReliabilityOptions? options = null)
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
        DescribeNotification<PortfolioFinancialPolicyCreatedEvent, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)),
        DescribeNotification<PortfolioFinancialPolicyVersionAddedEvent, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)),
        DescribeNotification<PortfolioFinancialPolicyActivatedEvent, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)),
        DescribeNotification<PortfolioFinancialPolicyRetiredEvent, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)),
        DescribeNotification<DraftPortfolioFinancialPolicyDeletedEvent, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)),
    ];

    public override IReadOnlyCollection<Type> ProjectedEventTypes => Types;
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;
}
