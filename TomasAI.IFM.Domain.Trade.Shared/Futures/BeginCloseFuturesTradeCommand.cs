using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures;
[MessagePackObject]
public sealed record BeginCloseFuturesTradeCommand : EstablishedTradeCommand
{ public const string Verb="BeginCloseFuturesTrade"; [IgnoreMember] public override BoundedContextName RouteTo => BoundedContextName.FuturesTradeBoundedContext; }
