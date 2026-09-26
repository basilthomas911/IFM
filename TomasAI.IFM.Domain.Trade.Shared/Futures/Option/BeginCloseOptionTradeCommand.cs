using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures.Option;
[MessagePackObject]
public sealed record BeginCloseOptionTradeCommand : EstablishedTradeCommand
{ public const string Verb="BeginCloseOptionTrade"; [IgnoreMember] public override BoundedContextName RouteTo => BoundedContextName.OptionTradeBoundedContext; }
