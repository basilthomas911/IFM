using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Trade.Shared.Order;
/// <summary>Read-only EOD presentation values from already queried backend evidence.
/// This query does not authorize a lifecycle transition or replace ledger state.</summary>
[MessagePackObject]
public sealed record TradeEndOfDayDisplayRequest(
 [property:Key(0)] StrategyPositionSnapshot Position,
 [property:Key(1)] decimal AvailableCash);
[MessagePackObject]
public sealed record TradeEndOfDayDisplay(
 [property:Key(0)] StrategyPositionId PositionId,
 [property:Key(1)] long PositionSequence,
 [property:Key(2)] decimal TradePnl,
 [property:Key(3)] decimal FundBalance);
[MessagePackObject]
public sealed record GetTradeEndOfDayDisplayQuery:IQuery<TradeEndOfDayDisplay>{
 public const string Actor=TradeOrderActorNames.Query;
 public const string Verb="GetTradeEndOfDayDisplay";
 [Key(0)] public ActorSubject Subject{get;init;}
 [Key(1)] public IActorEntityId EntityId{get;init;}=ActorEntityId.Default;
 [Key(2)] public TradeEndOfDayDisplayRequest Request{get;init;}=null!;
 [IgnoreMember] public int ErrorCode=>25214;
 [IgnoreMember] public string? QueryParams=>null;
}
