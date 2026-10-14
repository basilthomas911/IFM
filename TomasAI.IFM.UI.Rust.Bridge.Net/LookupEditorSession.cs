using TomasAI.IFM.UI.Net.Models.Reference;
using TomasAI.IFM.UI.Net.Services.Reference;
using TomasAI.IFM.UI.Net.ViewModels.Reference;
namespace TomasAI.IFM.UI.Rust.Bridge.Net;
/// <summary>Adapts the unchanged lookup view model; completion means its correlated terminal notification was received.</summary>
sealed class LookupEditorSession : IDisposable
{
 readonly RustClientServiceScope scope;
 readonly LookupTypeEditorViewModel model;
 readonly Task ready;
 public LookupEditorSession(){scope=new(Environment.GetEnvironmentVariable("IFM_UI_NATS_URL")??"nats://localhost:4222");model=new(scope,scope.Container.GetInstance<IReferenceDataService>());ready=scope.StartAsync().AsTask();}
 public async Task<LookupTypeUiModel[]> ExecuteAsync(EditorIntent intent)
 {
  await ready;
  switch(intent.Intent)
  {
   case "load":await model.LoadLookupTypes();break;
   case "add":await model.InitializeAsync(CancellationToken.None);await ObserveAsync(model.AddLookupType(intent.Lookup!,()=>{}));break;
   case "change":await model.InitializeAsync(CancellationToken.None);await ObserveAsync(model.ChangeLookupType(intent.Lookup!.LookupTypeName,intent.Lookup.OrderId,intent.Lookup,false,()=>{}));break;
   case "remove":await model.InitializeAsync(CancellationToken.None);await ObserveAsync(model.RemoveLookupType(intent.Lookup!.LookupTypeName,intent.Lookup.OrderId,false));break;
  }
  return model.LookupTypes.Values.OrderBy(x=>x.LookupTypeName,StringComparer.Ordinal).ThenBy(x=>x.OrderId).ToArray();
 }
 async Task ObserveAsync(Task operation)
 {
  try{await operation.WaitAsync(TimeSpan.FromSeconds(30));}
  catch(TimeoutException){await model.StopAsync(CancellationToken.None);try{await operation;}catch(Exception error){Console.Error.WriteLine("Lookup observation ended after timeout: "+error.Message);}throw new EditorOutcomeUnknownException("Command completion was not observed. Reload and verify the definition before retrying.");}
 }
 public void Dispose(){model.DisposeAsync().AsTask().GetAwaiter().GetResult();scope.DisposeAsync().AsTask().GetAwaiter().GetResult();}
}
