using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures.Option;

[MessagePackObject]
public sealed record AmendOptionTradeEvidenceCommand : AmendEstablishedTradeEvidenceCommand
{
    public const string Verb = "AmendOptionTradeEvidence";
    [IgnoreMember] public override BoundedContextName RouteTo => BoundedContextName.OptionTradeBoundedContext;
    /// <summary>Exact existing legs with corrected contract cash multipliers; every other leg field is immutable.</summary>
    [Key(6)] public TradeLegDefinition[] CorrectedLegs { get; init; } = [];
    /// <summary>The evidence revision verified by the reconciliation producer.</summary>
    [Key(7)] public int ExpectedEvidenceRevision { get; init; }
    /// <summary>The reason retained in the command audit alongside its resulting full-state event.</summary>
    [Key(8)] public string ReconciliationReason { get; init; } = string.Empty;
    /// <summary>Verified contract-reference and ledger reconciliation evidence identities.</summary>
    [Key(9)] public string ReconciliationEvidence { get; init; } = string.Empty;
}
