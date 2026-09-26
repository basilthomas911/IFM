using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures;
[MessagePackObject]
public sealed record CloseFuturesTradeCommand : EstablishedTradeCommand
{
    public const string Verb = "CloseFuturesTrade";
    [Key(4)] public ExecutionFillEvidence[] ClosingFills { get; init; } = [];
    [Key(5)] public DateTime ClosedAtUtc { get; init; }
    [IgnoreMember] public override BoundedContextName RouteTo => BoundedContextName.FuturesTradeBoundedContext;
}
