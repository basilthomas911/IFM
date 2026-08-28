using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Realtime.Actor;

/// <summary>Defines the stateless Regime terminal-translation actor context.</summary>
public interface IRegimeDiscoveryPipelineRealtimeContext
    : IRealtimeActorContext<RegimeDiscoveryPipelineRealtimeActor>
{
    /// <summary>Gets the actor logger.</summary>
    ILogger<RegimeDiscoveryPipelineRealtimeActor> Logger { get; }
}

/// <summary>Provides the closed-generic Regime terminal-translation context.</summary>
public sealed class RegimeDiscoveryPipelineRealtimeContext
    : EventActorContext,
      IRealtimeActorContext<RegimeDiscoveryPipelineRealtimeActor>,
      IRegimeDiscoveryPipelineRealtimeContext
{
    /// <summary>Initializes the stateless realtime context.</summary>
    public RegimeDiscoveryPipelineRealtimeContext(
        IActorSupervisor supervisor,
        ILogger<RegimeDiscoveryPipelineRealtimeActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Realtime, RegimeDiscoveryPipelineRealtimeActor.ActorName))
        => Logger = IsArgumentNull.Set(logger);

    /// <inheritdoc />
    public ILogger<RegimeDiscoveryPipelineRealtimeActor> Logger { get; }
}
