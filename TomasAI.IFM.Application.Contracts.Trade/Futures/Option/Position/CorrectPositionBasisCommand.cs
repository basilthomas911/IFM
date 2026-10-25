using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
[MessagePackObject]
[Union(0, typeof(CorrectIronCondorPositionBasisCommand))]
[Union(1, typeof(CorrectVerticalSpreadPositionBasisCommand))]
public abstract record CorrectPositionBasisCommand : PositionCommand
{
    [Key(4)] public Guid TradeLegId { get; init; }
    [Key(5)] public decimal Price { get; init; }
    [Key(6)] public DateTime EffectiveAtUtc { get; init; }
}
