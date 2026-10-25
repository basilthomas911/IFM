using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.Json.Serialization;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
/// <summary>Development-only serialization inventory for the embedded evidence
/// pane. Runtime Rust consumes generated metadata, never a managed serializer.</summary>
internal static class InspectionJsonFixtures {
 public static void Export(string output){
  var options=new JsonSerializerOptions{TypeInfoResolver=new DefaultJsonTypeInfoResolver()};
  var shapes=new SortedDictionary<string,object>();var seen=new HashSet<Type>();
  void Visit(Type type){
   if(!seen.Add(type))return;
   if(Nullable.GetUnderlyingType(type) is {} inner){Visit(inner);return;}
   if(type.IsArray){Visit(type.GetElementType()!);return;}
   if(type.IsGenericType)foreach(var argument in type.GetGenericArguments())Visit(argument);
   if(type.IsPrimitive||type.IsEnum||type==typeof(string)||type==typeof(decimal)||type==typeof(Guid)||type==typeof(DateOnly)||type==typeof(DateTime)||type==typeof(DateTimeOffset))return;
   var info=options.GetTypeInfo(type);
   if(info.Kind!=JsonTypeInfoKind.Object)return;
   var properties=info.Properties.Where(p=>p.Get is not null).Select(p=>new{name=p.Name,member=(p.AttributeProvider as MemberInfo)?.Name??p.Name,type=p.PropertyType.ToString(),ignoreNull=(p.AttributeProvider as MemberInfo)?.GetCustomAttribute<JsonIgnoreAttribute>()?.Condition==JsonIgnoreCondition.WhenWritingNull,ignoreDefault=(p.AttributeProvider as MemberInfo)?.GetCustomAttribute<JsonIgnoreAttribute>()?.Condition==JsonIgnoreCondition.WhenWritingDefault}).ToArray();
   shapes[type.ToString()]=properties;
   foreach(var property in info.Properties.Where(p=>p.Get is not null))Visit(property.PropertyType);
  }
  Visit(typeof(RiskObservation));
  FixtureFiles.WriteAllText(Path.Combine(output,"inspection-json-shapes.json"),JsonSerializer.Serialize(shapes));
  foreach(var name in new[]{"risk-observation","risk-completed"}){
   var bytes=File.ReadAllBytes(Path.Combine(output,name+".msgpack"));
   var transported=MessagePack.MessagePackSerializer.Deserialize<RiskObservation>(bytes,TomasAI.IFM.Framework.Serialization.MessagePackBinarySerializer.Options);
   FixtureFiles.WriteAllText(Path.Combine(output,name+"-embedded-json.txt"),JsonSerializer.Serialize(transported,new JsonSerializerOptions{WriteIndented=true}));
  }
 }
}
