using System.Reflection;
using System.Text.Json;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.UI.Rust.Bridge.Net;
/// <summary>Runs the unchanged hybrid session against an in-memory API; no CLR code is shipped with Rust.</summary>
public class ParameterFixtureProxy:DispatchProxy {
 internal readonly ParameterResponder Scenario=new();
 protected override object? Invoke(MethodInfo? method,object?[]? args){
  var value=method!.Name switch{
   "ComponentsAsync"=>(object)Scenario.Components,
   "VersionsAsync"=>Scenario.Versions((string)args![2]!),
   "PreviewValuesAsync"=>ParameterResponder.Preview((Guid)args![0]!, (string)args[1]!, (TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType)(int)args[2]!,args[3] as ParameterSetValue),
   _=>throw new NotSupportedException(method.Name)
  };
  var serviceType=method.ReturnType.GetGenericArguments()[0];var service=Activator.CreateInstance(serviceType)!;serviceType.GetProperty("Success")!.SetValue(service,true);serviceType.GetProperty("ErrorMessage")!.SetValue(service,"");serviceType.GetProperty("Value")!.SetValue(service,value);
  return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(serviceType).Invoke(null,[service]);
 }
}
internal static class ParameterSessionFixtures {
 public static async Task Export(string output){
  var api=DispatchProxy.Create<IParameterSetsApi,ParameterFixtureProxy>();await using var session=new ParameterSetSession(api);
  var publication=await session.ExecuteAsync(new(1,Guid.Parse("c8b3ef3f-d1b9-465a-b8ad-e96dd45b6a93"),"parameter-sets","load",0,null,[]));
  FixtureFiles.WriteAllText(Path.Combine(output,"parameter-initial-display.json"),JsonSerializer.Serialize(new{rows=publication.Rows,choices=publication.Choices,context=publication.Context}));
  foreach(var(key,bytes)in publication.Documents)FixtureFiles.WriteAllBytes(Path.Combine(output,$"parameter-initial-{key}.msgpack"),bytes);
 }
}
