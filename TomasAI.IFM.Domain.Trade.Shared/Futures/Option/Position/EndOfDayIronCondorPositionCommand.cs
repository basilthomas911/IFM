using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
[MessagePackObject] public sealed record EndOfDayIronCondorPositionCommand : TimedPositionCommand { public const string Verb="EndOfDayIronCondorPosition"; [IgnoreMember] public override BoundedContextName RouteTo => BoundedContextName.FuturesIronCondorTradePositionBoundedContext; }
