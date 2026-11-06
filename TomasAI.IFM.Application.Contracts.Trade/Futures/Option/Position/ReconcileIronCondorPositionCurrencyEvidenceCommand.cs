using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;

/// <summary>Identifies one explicit, version-fenced repair of legacy position evidence.</summary>
[MessagePackObject]
public sealed record ReconcileIronCondorPositionCurrencyEvidenceCommand : PositionCommand
{
    public const string Verb = "ReconcileIronCondorPositionCurrencyEvidence";
    [Key(4)] public long ExpectedStreamVersion { get; init; }
    [IgnoreMember] public override BoundedContextName RouteTo => BoundedContextName.FuturesIronCondorTradePositionBoundedContext;
}
