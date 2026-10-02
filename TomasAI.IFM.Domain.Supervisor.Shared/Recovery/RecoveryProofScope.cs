namespace TomasAI.IFM.Domain.Supervisor.Shared.Recovery;

/// <summary>Identifies the boundary actually traversed by a recovery proof.</summary>
public enum RecoveryProofScope
{
    /// <summary>Supervisor command, JetStream event, and Supervisor-owned projector only.</summary>
    SupervisorControlPlane = 0,
    /// <summary>Reserved for proof through the actual market-data publisher and required business projectors.</summary>
    MarketDataDownstream = 1
}
