using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
namespace TomasAI.IFM.Domain.Trade.Shared.Futures.Position;
/// <summary>Requests a clock-based plan evaluation; it does not change market observations or position state.</summary>
[MessagePackObject]
public sealed record FuturesPositionExitClockEvent : PositionChangedEvent
{
    public new const string Verb = "EvaluateFuturesExitClock";
}
