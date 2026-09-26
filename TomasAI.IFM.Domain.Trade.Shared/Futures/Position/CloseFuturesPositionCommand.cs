using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures.Position;

[MessagePackObject]
public sealed record CloseFuturesPositionCommand : FuturesPositionCommand
{
    public const string Verb = "CloseFuturesPosition";
    [Key(4)] public DateTime EffectiveAtUtc { get; init; }
    private ExecutionFillEvidence[] closingFills = [];
    [Key(5)] public ExecutionFillEvidence[] ClosingFills { get => closingFills; init => closingFills = value ?? []; }
}
