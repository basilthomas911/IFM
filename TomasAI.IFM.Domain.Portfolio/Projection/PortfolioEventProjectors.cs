using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Command.Actor;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventProjector;

namespace TomasAI.IFM.Domain.Portfolio.Projection;

public sealed class PortfolioEventProjector(
    IDurableReplayQueue replayQueue, IEventSourceActorDbContext eventSource, IBlackboardService blackboard,
    ILogger<PortfolioEventProjector> logger, IPortfolioEventStore events, IPortfolioDbContext projections,
    EventProjectorReliabilityOptions? options = null)
    : ConventionalEventProjector<PortfolioCommandActor>(replayQueue, eventSource, blackboard, logger, options)
{
    static readonly ImmutableArray<Type> Types =
    [
        typeof(PortfolioCreated), typeof(PortfolioVersionAdded), typeof(PortfolioOperatingStateChanged),
        typeof(FundAddedToPortfolio), typeof(PortfolioRetired), typeof(FundAllocationDelegated),
        typeof(FundRiskEnvelopeDelegated),
    ];
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors =
        [
            DescribeNotification<PortfolioCreated, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)),
            DescribeNotification<PortfolioVersionAdded, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)),
            DescribeNotification<PortfolioOperatingStateChanged, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)),
            DescribeNotification<FundAddedToPortfolio, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)),
            DescribeNotification<PortfolioRetired, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)),
            DescribeNotification<FundAllocationDelegated, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)),
            DescribeNotification<FundRiskEnvelopeDelegated, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)),
        ];

    public override IReadOnlyCollection<Type> ProjectedEventTypes => Types;
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;
}

public sealed class PortfolioFundEventProjector(
    IDurableReplayQueue replayQueue, IEventSourceActorDbContext eventSource, IBlackboardService blackboard,
    ILogger<PortfolioFundEventProjector> logger, IPortfolioEventStore events, IPortfolioDbContext projections,
    EventProjectorReliabilityOptions? options = null)
    : ConventionalEventProjector<PortfolioFundCommandActor>(replayQueue, eventSource, blackboard, logger, options)
{
    static readonly ImmutableArray<Type> Types =
    [
        typeof(FundMandateCreated), typeof(FundMandateVersionAdded), typeof(FundOperatingStateChanged),
        typeof(FundTradeTemplateAssigned), typeof(FundCompositionReserved), typeof(FundCompositionStateChanged),
    ];
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors =
        [
            DescribeNotification<FundMandateCreated, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)),
            DescribeNotification<FundMandateVersionAdded, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)),
            DescribeNotification<FundOperatingStateChanged, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)),
            DescribeNotification<FundTradeTemplateAssigned, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)),
            DescribeNotification<FundCompositionReserved, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)),
            DescribeNotification<FundCompositionStateChanged, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)),
        ];

    public override IReadOnlyCollection<Type> ProjectedEventTypes => Types;
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;
}
