using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using MessagePack;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order;
internal static class TradeLifecycleResponder {
 static string mode="Normal";static readonly Dictionary<Guid,byte[]> commands=[];static readonly HashSet<string> stages=[];
 internal static Guid? Attempt;
 public static bool Handles(string subject)=>subject.StartsWith("Query.NativeCompatibility.TradeLifecycle",StringComparison.Ordinal)||subject.StartsWith("Command.TradeOrderCommand.",StringComparison.Ordinal);
 public static byte[]? Handle(string subject,byte[] payload){
  var options=MessagePackBinarySerializer.Options;
  byte[] Result<T>(T value)=>MessagePackSerializer.Serialize(new ServiceResult<T>{Success=true,ErrorMessage="",Value=value},options);
  if(subject=="Query.NativeCompatibility.TradeLifecycleMode.none"){mode=MessagePackSerializer.Deserialize<string>(payload,options);commands.Clear();stages.Clear();Attempt=null;return Result(new ScalarReadModel<bool>(true));}
  if(subject=="Query.NativeCompatibility.TradeLifecycleCount.none")return Result(new ScalarReadModel<int>(commands.Count));
  var verb=subject.Split('.')[2];var type=typeof(CreateTradeOrderCommand).Assembly.GetTypes().Single(t=>t.Namespace==typeof(CreateTradeOrderCommand).Namespace&&t.GetField("Verb",BindingFlags.Public|BindingFlags.Static)?.GetRawConstantValue() as string==verb);
  var c=MessagePackSerializer.Deserialize(type,payload,options)!;
  T Field<T>(string name)=>(T)type.GetProperty(name)!.GetValue(c)!;
  var entity=Field<TradeOrderId>("EntityId");var id=Field<Guid>("CommandId");var route=Field<TomasAI.IFM.Shared.EventModelActor.ActorSubject>("Subject");
  if(entity!=OrderPreparationResponder.Accepted?.Id||route.ToString()!=subject||!Field<bool>("PostEvents"))throw new InvalidDataException("Incorrect native lifecycle scope");
  Guid Stable(params string[] values)=>new(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|',values))).AsSpan(0,16));
  var seed=Stable("95112233445566778899aabbccddeeff",entity.Format());
  var stage=verb switch{"CreateTradeOrder"=>"create","ApproveTradeOrder"=>"approve","ReadyTradeOrder"=>"ready","BindTradeOrderExecution"=>"bind",_=>throw new InvalidDataException("Unsupported lifecycle verb")};
  if(id!=Stable(seed.ToString("N"),stage))throw new InvalidDataException("Native lifecycle identity differs from unchanged client");
  if(commands.TryGetValue(id,out var prior)){if(!prior.SequenceEqual(payload))throw new InvalidDataException("Lifecycle retry changed its retained bytes");return Result(new GuidResult(id));}
  if(stage=="create"){
   var order=Field<TradeOrderDefinition>("Order");if(OrderPreparationResponder.Accepted is not {} accepted||!MessagePackSerializer.Serialize(order,options).SequenceEqual(MessagePackSerializer.Serialize(accepted,options)))throw new InvalidDataException("Accepted instruction mapping differs from unchanged mapper");
  }
  if(stage=="bind"){
   if(Field<DateTime>("EffectiveAtUtc")!=StrategyFixtures.At||Field<Guid>("ExecutionAttemptId")!=Stable(seed.ToString("N"),"execution-attempt"))throw new InvalidDataException("Native binding identity/time differs");Attempt=Field<Guid>("ExecutionAttemptId");
  }
  if(stage!="create"&&!stages.Contains(stage=="approve"?"create":stage=="ready"?"approve":"ready"))throw new InvalidDataException("Lifecycle stage ran out of order");
  commands[id]=payload;stages.Add(stage);
  if(mode=="LostAck"&&stage=="create")return null;
  return Result(new GuidResult(id));
 }
}
