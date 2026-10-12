using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
[MessagePackObject]
[Union(0, typeof(EndOfDayIronCondorPositionCommand))]
[Union(1, typeof(CloseIronCondorPositionCommand))]
[Union(2, typeof(EndOfDayVerticalSpreadPositionCommand))]
[Union(3, typeof(CloseVerticalSpreadPositionCommand))]
public abstract record TimedPositionCommand : PositionCommand
{
    [Key(4)] public DateTime EffectiveAtUtc { get; init; }
}
