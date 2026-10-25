using MessagePack;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Domain.Trade.Order.Query.Model;
internal static class EodResponder{
 static string mode="Disabled";static bool sealedPosition;static int commands;
 static readonly Dictionary<Guid,byte[]> seen=[];
 public static bool Handles(string s)=>s.StartsWith("Query.NativeCompatibility.Eod",StringComparison.Ordinal)||mode!="Disabled"&&(s.StartsWith("Query.FuturesOptionTradePositionQuery.GetIronCondorOptionTradePosition.",StringComparison.Ordinal)||s.StartsWith("Query.TradeOrderQuery.GetTradeEndOfDayDisplay.",StringComparison.Ordinal)||s.StartsWith("Command.FuturesIronCondorTradePositionCommand.EndOfDayIronCondorPosition.",StringComparison.Ordinal));
 public static byte[]? Handle(string s,byte[] bytes){
  var o=MessagePackBinarySerializer.Options;byte[] Result<T>(T v)=>MessagePackSerializer.Serialize(new ServiceResult<T>{Success=true,Value=v,ErrorMessage=""},o);
  if(s=="Query.NativeCompatibility.EodMode.none"){mode=MessagePackSerializer.Deserialize<string>(bytes,o);sealedPosition=false;commands=0;seen.Clear();return Result(new ScalarReadModel<bool>(true));}
  if(s=="Query.NativeCompatibility.EodCount.none")return Result(new ScalarReadModel<int>(commands));
  if(s.StartsWith("Query.TradeOrderQuery",StringComparison.Ordinal)){
   var q=MessagePackSerializer.Deserialize<GetTradeEndOfDayDisplayQuery>(bytes,o);if(q.Subject.ToString()!=s||q.Request.Position.Id!=MonitorFixtures.PositionId||q.Request.AvailableCash!=123456.78m)throw new InvalidDataException("EOD display evidence crossed scope");
   var display=TradeEndOfDayDisplayCompute.Calculate(q.Request);return Result(mode=="WrongDisplay"?display with{PositionSequence=999}:display);
  }
  if(s.StartsWith("Query.",StringComparison.Ordinal)){
   var q=MessagePackSerializer.Deserialize<GetIronCondorOptionTradePositionQuery>(bytes,o);if(q.PositionId!=MonitorFixtures.PositionId||q.Subject.ToString()!=s)throw new InvalidDataException("EOD current-position scope changed");
   return Result(MonitorFixtures.Mark with{Phase=sealedPosition?StrategyPositionPhase.EndOfDay:StrategyPositionPhase.MarkToMarket,LatestSealedValueDate=sealedPosition?MonitorFixtures.Date:default,RealizedPnl=10.25m,UnrealizedPnl=-60.50m});
  }
  var c=MessagePackSerializer.Deserialize<EndOfDayIronCondorPositionCommand>(bytes,o);var expected=new Guid(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"PositionEndOfDay:{MonitorFixtures.PositionId.Format()}:{MonitorFixtures.Date:yyyy-MM-dd}")).AsSpan(0,16));
  if(c.Subject.ToString()!=s||c.EntityId!=MonitorFixtures.PositionId||c.CommandId!=expected||c.ValueDate!=MonitorFixtures.Date||c.EffectiveAtUtc!=new DateTime(2026,10,26,23,59,59,DateTimeKind.Utc).AddTicks(9999999)||!c.PostEvents)throw new InvalidDataException("EOD command identity/date/boundary changed");
  if(seen.TryGetValue(c.CommandId,out var prior)&&!prior.SequenceEqual(bytes))throw new InvalidDataException("EOD retry changed command bytes");
  if(!seen.ContainsKey(c.CommandId)){commands++;seen[c.CommandId]=bytes;}sealedPosition=true;
  return mode=="LostAck"?null:Result(new GuidResult(c.CommandId));
 }
}
