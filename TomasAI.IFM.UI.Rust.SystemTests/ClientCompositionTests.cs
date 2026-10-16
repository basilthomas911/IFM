using TomasAI.IFM.UI.Rust.Bridge.Net;
using TomasAI.IFM.UI.Net.Services.Reference;
using TomasAI.IFM.UI.Net.Services.SystemAdmin;
using Xunit;
public class ClientCompositionTests
{
 [Fact] public async Task Both_ui_hosts_can_use_the_same_typed_service_registrations()
 {
  await using var scope=new RustClientServiceScope("nats://localhost:4222");
  Assert.NotNull(scope.Services.StrategyOperations);
  Assert.NotNull(scope.Container.GetInstance<IReferenceDataService>());
  Assert.NotNull(scope.Container.GetInstance<IScheduledTaskService>());
  Assert.NotNull(scope.Container.GetInstance<IDatabaseBackupService>());
  Assert.Equal("Development",scope.AppEnvironment);
  bool ran=false;await scope.ExecuteAsync(_=>{ran=true;return Task.CompletedTask;});Assert.True(ran);
 }
}
