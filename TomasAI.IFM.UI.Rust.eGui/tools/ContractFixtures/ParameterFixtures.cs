using System.Reflection;
using System.Text.Json;
using MessagePack;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.UI.Net.ViewModels.Reference.ParameterSets;
using TomasAI.IFM.UI.Rust.Bridge.Net;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
/// <summary>Read/apply oracle for every concrete parameter union; no domain actors or persistence.</summary>
internal static class ParameterFixtures {
 public static ParameterSetValue[] Values()=>typeof(ParameterSetValue).GetCustomAttributes<UnionAttribute>().OrderBy(v=>v.Key).Select(union=>{
  var type=union.SubType.GetProperty("Value")!.PropertyType;
  var value=Activator.CreateInstance(type)!;
  type.GetProperty("ParameterSetId")!.SetValue(value,Guid.Parse("f256f7a9-b41c-4fb9-9ec3-eab3497bc86a"));
  return (ParameterSetValue)Activator.CreateInstance(union.SubType,value)!;
 }).ToArray();
 public static void Export(string output){
  var templates=typeof(ParameterSetValue).Assembly.GetTypes().Where(t=>t.Namespace==typeof(ParameterSetValue).Namespace&&!t.IsAbstract&&t.GetConstructor(Type.EmptyTypes) is not null&&(t.Name.EndsWith("Query",StringComparison.Ordinal)||t.Name.EndsWith("Command",StringComparison.Ordinal)))
    .ToDictionary(t=>t.Name,t=>MessagePackSerializer.Serialize(t,Activator.CreateInstance(t),MessagePackBinarySerializer.ContentOptions).Select(v=>(int)v).ToArray());
  FixtureFiles.WriteAllText(Path.Combine(output,"parameter-request-defaults.json"),JsonSerializer.Serialize(templates));
  var cases=new List<object>();var index=0;
  foreach(var original in Values()){
   var value=ParameterValueCodec.Snapshot(original);var name="parameter-values-"+index++;
   FixtureFiles.WriteAllBytes(Path.Combine(output,name+".msgpack"),MessagePackSerializer.Serialize(value,MessagePackBinarySerializer.Options));
   FixtureFiles.WriteAllText(Path.Combine(output,name+"-fields.json"),JsonSerializer.Serialize(BridgeDocument.Project(ParameterFieldEditorModel.Read(value))));
   FixtureFiles.WriteAllText(Path.Combine(output,name+"-all-fields.json"),JsonSerializer.Serialize(BridgeDocument.Project(ParameterFieldEditorModel.Read(value,true))));
   FixtureFiles.WriteAllText(Path.Combine(output,name+"-display.json"),JsonSerializer.Serialize(ParameterFieldEditorModel.Display(value)));
   var field=ParameterFieldEditorModel.Read(value).FirstOrDefault(v=>v.Kind is JsonValueKind.True or JsonValueKind.False);
   if(field is not null){var change=field with{Value=field.Value=="true"?"false":"true"};FixtureFiles.WriteAllText(Path.Combine(output,name+"-change.json"),JsonSerializer.Serialize(change));FixtureFiles.WriteAllBytes(Path.Combine(output,name+"-changed.msgpack"),MessagePackSerializer.Serialize(ParameterFieldEditorModel.Apply(value,[change]),MessagePackBinarySerializer.Options));}
   cases.Add(new{name,hasChange=field is not null});
  }
  FixtureFiles.WriteAllText(Path.Combine(output,"parameter-fields-cases.json"),JsonSerializer.Serialize(cases));
  var set=Guid.Parse("f256f7a9-b41c-4fb9-9ec3-eab3497bc86a");
  var legacy=Enum.GetValues<RegimeDiscoverySignalMetric>().Select((metric,index)=>new RegimeDiscoverySignalConfiguration{Metric=metric,TimeFrame=index%2==0?TimeFrameType.OneHour:TimeFrameType.Daily,Enabled=index%3!=0,IsRequired=true,MaximumAgeSeconds=100+index,PrepareAtStartup=index%4!=0,Monitor=index%5!=0}).ToArray();
  var signals=RegimeDiscoveryMetricConfigurationProjection.FromLegacySignals(set,legacy);var observations=RegimeDiscoveryMetricConfigurationProjection.FromLegacyObservations(set,legacy);
  var horizon=new RegimeDiscoveryHorizonConfiguration{TargetHorizon=TimeFrameType.Daily,TimeFrames=[new(){TimeFrame=TimeFrameType.OneHour,IsRequired=true,Weight=.5m,MaximumAgeSeconds=600},new(){TimeFrame=TimeFrameType.Daily,IsRequired=true,Weight=.5m,MaximumAgeSeconds=600}]};
  foreach(var(name,value)in new (string,object)[]{("legacy",legacy),("signals",signals),("observations",observations),("horizon",horizon),("converted",RegimeDiscoveryMetricConfigurationProjection.ToLegacyRequirements(set,signals,observations,horizon))})FixtureFiles.WriteAllText(Path.Combine(output,$"parameter-metrics-{name}.json"),JsonSerializer.Serialize(BridgeDocument.Project(value,enumNames:true)));
 }
}
