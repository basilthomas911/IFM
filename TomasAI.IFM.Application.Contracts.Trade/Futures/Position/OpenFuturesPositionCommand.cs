using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures.Position;

[MessagePackObject]
public sealed record OpenFuturesPositionCommand : FuturesPositionCommand
{
    public const string Verb = "OpenFuturesPosition";
    [Key(4)] public EstablishedTradeDefinition Trade { get; init; } = new();
    [Key(5)] public DateTime EffectiveAtUtc { get; init; }
}
