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
    EventProjectorReliabilityOptions? options = null)
    : ConventionalEventProjector<PortfolioCommandActor>(replayQueue, eventSource, blackboard, logger, options)
{
    static readonly ImmutableArray<Type> Types =
    [
        typeof(PortfolioCreatedEvent), typeof(PortfolioVersionAddedEvent), typeof(PortfolioOperatingStateChangedEvent),
        typeof(FundAddedToPortfolioEvent), typeof(PortfolioRetiredEvent), typeof(FundAllocationDelegatedEvent),
        typeof(FundRiskEnvelopeDelegatedEvent),
        typeof(DraftPortfolioDeletedEvent),
    ];
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors =
        [
            DescribeNotification<PortfolioCreatedEvent, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)),
            DescribeNotification<PortfolioVersionAddedEvent, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)),
            DescribeNotification<PortfolioOperatingStateChangedEvent, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)),
            DescribeNotification<FundAddedToPortfolioEvent, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)),
            DescribeNotification<PortfolioRetiredEvent, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)),
            DescribeNotification<FundAllocationDelegatedEvent, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)),
            DescribeNotification<FundRiskEnvelopeDelegatedEvent, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)),
            DescribeNotification<DraftPortfolioDeletedEvent, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)),
        ];

    public override IReadOnlyCollection<Type> ProjectedEventTypes => Types;
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;
}
