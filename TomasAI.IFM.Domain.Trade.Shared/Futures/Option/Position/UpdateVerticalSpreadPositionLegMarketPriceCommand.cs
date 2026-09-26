using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
[MessagePackObject] public sealed record UpdateVerticalSpreadPositionLegMarketPriceCommand : UpdatePositionLegMarketPriceCommand { public const string Verb="UpdateVerticalSpreadPositionLegMarketPrice"; [IgnoreMember] public override BoundedContextName RouteTo => BoundedContextName.FuturesVerticalSpreadTradePositionBoundedContext; }
