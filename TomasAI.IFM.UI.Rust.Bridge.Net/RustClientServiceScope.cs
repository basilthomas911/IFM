using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SimpleInjector;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.UI.Net.Composition;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Services;
namespace TomasAI.IFM.UI.Rust.Bridge.Net;
/// <summary>Owns the same client service composition used by WinForms, without forms or a WinForms message loop.</summary>
public sealed class RustClientServiceScope : IAppRoot,IAsyncDisposable
{
 public Container Container {get;}=new();
 readonly IActorProducer producer;
 public string AppEnvironment {get;}
 public Uri? LivePipelineHealthEndpoint {get;}
 public IUiServiceCatalog Services=>Container.GetInstance<IUiServiceCatalog>();
 public IValueDateProvider ValueDates=>Container.GetInstance<IValueDateProvider>();
 public ILogger DiagnosticLogger {get;}
 public RustClientServiceScope(string natsUrl,string environment="Development",ILogger? logger=null)
 {
  AppEnvironment=environment;DiagnosticLogger=logger??NullLogger.Instance;
  Container.RegisterInstance<ILogger>(DiagnosticLogger);
  Container.RegisterInstance<ILogger<TomasAI.IFM.Shared.EventChannel.EventChannel>>(NullLogger<TomasAI.IFM.Shared.EventChannel.EventChannel>.Instance);
  Container.RegisterInstance<IAppRoot>(this);
  var configBuilder=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["AppSettings:NatsServerUri"]=natsUrl,["MarketDataOperationsHealth:Endpoint"]="http://localhost:22543/api/market-data/operations-health"});
  var settings=Path.Combine(Path.GetDirectoryName(typeof(RustClientServiceScope).Assembly.Location)!,"ui-client.Development.json");
  if(environment=="Development" && File.Exists(settings))configBuilder.AddJsonFile(settings,optional:false,reloadOnChange:false);
  var config=configBuilder.Build();
  LivePipelineHealthEndpoint=Uri.TryCreate(config["MarketDataOperationsHealth:Endpoint"],UriKind.Absolute,out var healthEndpoint)?new Uri(healthEndpoint,"live-health"):null;
  new UiClientServiceRegistration(Container,config).Register();
  Container.Verify();
  producer=Container.GetInstance<IActorProducer>();
 }
 public ValueTask StartAsync(CancellationToken cancellationToken=default)=>producer.StartAsync(new(ActorType.Query,"IFM.Rust.Client"),cancellationToken);
 public IStatusConsoleWriter GetStatusConsoleWriter()=>Container.GetInstance<IStatusConsoleWriter>();
 public Task ExecuteAsync(Func<CancellationToken,Task> operation,CancellationToken cancellationToken=default){ArgumentNullException.ThrowIfNull(operation);cancellationToken.ThrowIfCancellationRequested();return operation(cancellationToken);}
 public async ValueTask DisposeAsync(){await producer.StopAsync();await Container.DisposeAsync();}
}
