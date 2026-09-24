using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Command.Actor;
using TomasAI.IFM.Domain.Portfolio.Fund.Command.Actor;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.FinancialPolicy.Events;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventProjector;

namespace TomasAI.IFM.Domain.Portfolio.Projection;

public sealed class PortfolioFundEventProjector(
    IDurableReplayQueue replayQueue, IEventSourceActorDbContext eventSource, IBlackboardService blackboard,
    ILogger<PortfolioFundEventProjector> logger, IPortfolioEventStore events, IPortfolioDbWriteContext projections,
    EventProjectorReliabilityOptions? options = null)
    : ConventionalEventProjector<PortfolioFundCommandActor>(replayQueue, eventSource, blackboard, logger, options)
{
    static readonly ImmutableArray<Type> Types =
    [
        typeof(FundMandateCreatedEvent), typeof(FundMandateVersionAddedEvent), typeof(FundOperatingStateChangedEvent),
        typeof(FundTradeTemplateAssignedEvent), typeof(FundCompositionReservedEvent), typeof(FundCompositionStateChangedEvent),
        typeof(FundManualOrderChangedEvent), typeof(FundManualOrderDeletedEvent),
    ];
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors =
        [
            DescribeNotification<FundMandateCreatedEvent, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)),
            DescribeNotification<FundMandateVersionAddedEvent, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)),
            DescribeNotification<FundOperatingStateChangedEvent, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)),
            DescribeNotification<FundTradeTemplateAssignedEvent, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)),
            DescribeNotification<FundCompositionReservedEvent, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)),
            DescribeNotification<FundCompositionStateChangedEvent, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)),
            DescribeNotification<FundManualOrderChangedEvent, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)),
            DescribeNotification<FundManualOrderDeletedEvent, ActorEntityId>(x => new PortfolioProjectionHandler(events, projections).ApplyAsync(x)),
        ];

    public override IReadOnlyCollection<Type> ProjectedEventTypes => Types;
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;
}
