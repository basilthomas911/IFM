using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures;
[MessagePackObject]
public sealed record AmendFuturesTradeEvidenceCommand : AmendEstablishedTradeEvidenceCommand
{ public const string Verb="AmendFuturesTradeEvidence"; [IgnoreMember] public override BoundedContextName RouteTo => BoundedContextName.FuturesTradeBoundedContext; }
