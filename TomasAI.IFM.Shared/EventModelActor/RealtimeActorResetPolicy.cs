namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>Restricts non-draining replacement to market-data setup actors; financial actors retain durable recovery.</summary>
public static class RealtimeActorResetPolicy
{
    /// <summary>Checks the explicit domain boundary for disposable actor replacement.</summary>
    public static bool CanReplace(Type type, ActorType actorType)
        
    {
        ArgumentNullException.ThrowIfNull(type);
        return actorType == ActorType.Realtime
            && (type.Namespace?.StartsWith("TomasAI.IFM.Domain.MarketData.Feed.", StringComparison.Ordinal) == true
                || type.Namespace?.StartsWith("TomasAI.IFM.Domain.MarketData.Analytics.", StringComparison.Ordinal) == true);
    }
}
