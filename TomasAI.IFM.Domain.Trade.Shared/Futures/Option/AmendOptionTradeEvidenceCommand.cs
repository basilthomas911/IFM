using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures.Option;
[MessagePackObject]
public sealed record AmendOptionTradeEvidenceCommand : AmendEstablishedTradeEvidenceCommand
{ public const string Verb="AmendOptionTradeEvidence"; [IgnoreMember] public override BoundedContextName RouteTo => BoundedContextName.OptionTradeBoundedContext; }
