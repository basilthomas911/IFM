using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.CapacityReservation.Emulator;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.CapacityReservation.Emulator;

/// <summary>Durably projects committed financial history and publishes completion notifications.</summary>
public sealed class EmulatorExecutionProjector(IDurableReplayQueue queue,IEventSourceActorDbContext eventSource,IBlackboardService blackboard,
    IFinancialHistoryProjection history,ILogger<EmulatorExecutionProjector> logger,EventProjectorReliabilityOptions? options=null)
    :ConventionalEventProjector<EmulatorExecutionCommandActor>(queue,eventSource,blackboard,logger,options)
{
    static readonly ImmutableArray<Type> Types=[typeof(EmulatorOrderSubmittedEvent)];
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors=[
        DescribeNotification<EmulatorOrderSubmittedEvent,LedgerPortfolioId>(value=>history.ApplyAsync(value))
    ];
    public override IReadOnlyCollection<Type> ProjectedEventTypes=>Types;
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors=>_descriptors;
}

