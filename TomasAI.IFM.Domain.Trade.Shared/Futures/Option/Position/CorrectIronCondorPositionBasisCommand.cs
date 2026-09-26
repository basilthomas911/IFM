using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
[MessagePackObject] public sealed record CorrectIronCondorPositionBasisCommand : CorrectPositionBasisCommand { public const string Verb="CorrectIronCondorPositionBasis"; [IgnoreMember] public override BoundedContextName RouteTo => BoundedContextName.FuturesIronCondorTradePositionBoundedContext; }
