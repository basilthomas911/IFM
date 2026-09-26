using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
[MessagePackObject] public sealed record UpdateIronCondorPositionLegMarketPriceCommand : UpdatePositionLegMarketPriceCommand { public const string Verb="UpdateIronCondorPositionLegMarketPrice"; [IgnoreMember] public override BoundedContextName RouteTo => BoundedContextName.FuturesIronCondorTradePositionBoundedContext; }
