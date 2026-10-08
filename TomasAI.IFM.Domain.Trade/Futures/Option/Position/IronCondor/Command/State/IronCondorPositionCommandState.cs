using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.State;

/// <summary>Owns authoritative PositionSnapshot state and pending source events.</summary>
public sealed class IronCondorPositionCommandState : BaseEventSourceActorState<IronCondorPositionCommandState>
{
    public override ActorThreadId Id { get; set; } = default!;
    public StrategyPositionSnapshot? PositionSnapshot { get; private set; }
    public StrategyPositionSnapshot? Current => PositionSnapshot;
    public TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradeLimitReadModel? TradeLimits { get; private set; }
    public TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradeTypeLimitReadModel[] SpreadLimits { get; private set; } = [];

    /// <summary>Applies supported source events to the authoritative business snapshot.</summary>
    protected override bool Apply(IEvent domainEvent)
    {
        switch (domainEvent)
        {
            case IronCondorPositionChangedEvent changed:
                PositionSnapshot = changed.PositionSnapshot;
                return true;
            case IronCondorMonitoringInitializedEvent initialized:
                TradeLimits = initialized.TradeLimits;
                SpreadLimits = initialized.SpreadLimits;
                return true;
            default:
                return false;
        }
    }
}
