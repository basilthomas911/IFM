using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketEvaluationSnapshot;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Realtime.Actor;

/// <summary>Defines the readonly runtime services required by <see cref="FuturesRsiSignalRealtimeActor"/>.</summary>
public interface IFuturesRsiSignalRealtimeContext : IRealtimeActorContext<FuturesRsiSignalRealtimeActor>
{
    /// <summary>Gets the Supervisor service supplied to the actor context.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the Projector service supplied to the actor context.</summary>
    IRealtimeProjector<FuturesRsiSignalRealtimeActor> Projector { get; }
    /// <summary>Gets the Blackboard service supplied to the actor context.</summary>
    IBlackboardService Blackboard { get; }
    /// <summary>Gets the Logger service supplied to the actor context.</summary>
    ILogger<FuturesRsiSignalRealtimeActor> Logger { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="FuturesRsiSignalRealtimeActor"/>.</summary>
public sealed class FuturesRsiSignalRealtimeContext : EventActorContext, IRealtimeActorContext<FuturesRsiSignalRealtimeActor>, IFuturesRsiSignalRealtimeContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public FuturesRsiSignalRealtimeContext(
        IActorSupervisor supervisor,
        IRealtimeProjector<FuturesRsiSignalRealtimeActor> projector,
        IBlackboardService blackboard,
        ILogger<FuturesRsiSignalRealtimeActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Realtime, FuturesRsiSignalRealtimeActor.ActorName))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        Projector = IsArgumentNull.Set(projector);
        Blackboard = IsArgumentNull.Set(blackboard);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public IRealtimeProjector<FuturesRsiSignalRealtimeActor> Projector { get; }
    /// <inheritdoc/>
    public IBlackboardService Blackboard { get; }
    /// <inheritdoc/>
    public ILogger<FuturesRsiSignalRealtimeActor> Logger { get; }
}
