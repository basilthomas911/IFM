using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures.Option;

[MessagePackObject]
public sealed record CloseOptionTradeCommand : EstablishedTradeCommand
{
    public const string Verb = "CloseOptionTrade";
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(4)] public ExecutionFillEvidence[] ClosingFills { get; init; } = [];
    [Key(5)] public DateTime ClosedAtUtc { get; init; }
    [IgnoreMember] public override BoundedContextName RouteTo => BoundedContextName.OptionTradeBoundedContext;
}
