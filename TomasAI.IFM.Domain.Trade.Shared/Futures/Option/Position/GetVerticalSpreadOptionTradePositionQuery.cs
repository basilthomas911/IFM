using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
[MessagePackObject]
public sealed record GetVerticalSpreadOptionTradePositionQuery : StrategyPositionQueryBase<StrategyPositionSnapshot>
{ public const string Verb = "GetVerticalSpreadOptionTradePosition"; }
