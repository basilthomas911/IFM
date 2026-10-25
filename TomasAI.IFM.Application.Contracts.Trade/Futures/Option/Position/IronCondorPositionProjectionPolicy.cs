using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;

/// <summary>Separates replaceable market observations from financial position transitions.</summary>
public static class IronCondorPositionProjectionPolicy
{
    /// <summary>Only market marks are disposable. Open, close, correction and other financial events retain recovery.</summary>
    public static bool IsDisposableMark(IEvent source)
        => source is IronCondorPositionChangedEvent { IsRealtimeMarketObservation: true,
            PositionSnapshot.Phase: StrategyPositionPhase.MarkToMarket };
}
