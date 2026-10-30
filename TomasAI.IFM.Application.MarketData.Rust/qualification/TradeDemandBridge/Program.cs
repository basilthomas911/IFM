// Actual managed adapter -> native owner qualification. No real provider, broker,
// database or credential is used. Deliberately unavailable source dependencies
// prove all four demands are admitted before the single readiness observer waits.
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Recovery;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Application.MarketData.Rust.Host;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Recovery;
using TomasAI.IFM.Framework.MarketData.DataBento;
var date=new DateOnly(2026,10,29);var at=new DateTimeOffset(2026,10,29,15,0,0,TimeSpan.Zero);
Environment.SetEnvironmentVariable("DATABENTO_API_KEY",new string('x',32));
var auditRoot=Environment.GetEnvironmentVariable("IFM_MARKETDATA_RECOVERY_OUTBOX");
var liveRoot=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"TomasAI","IFM","MarketData","RecoveryOutbox");
if(string.IsNullOrWhiteSpace(auditRoot)||Path.GetFullPath(auditRoot).StartsWith(liveRoot,StringComparison.OrdinalIgnoreCase))
 throw new Exception("Qualification did not isolate its recovery outbox before opening native resources.");
var configuration=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
 ["MarketDataHost:Rust:LibraryPath"]=Path.GetFullPath(args[0]),["MarketDataHost:Rust:AdvancingClock"]="false",
 ["MarketDataHost:NatsUrl"]="nats://127.0.0.1:1",["MarketDataHost:Scope"]="development",
 ["MarketDataHost:Rust:GlbxHost"]="127.0.0.1",["MarketDataHost:Rust:GlbxPort"]="1",
 ["MarketDataHost:Rust:DnsServers:0"]="127.0.0.1:1"}).Build();
var clock=new Clock(at);await using var cold=new NativeColdBackend(configuration,clock,NullLogger<NativeColdBackend>.Instance);
var options=new DatabentoMarketDataRuntimeOptions{Contracts=[],FeedOptions=DatabentoFeedOptions.ForProfile(FeedDeploymentProfile.Development,"GLBX.MDP3")};
await using var backend=new NativeDatasetBackend(cold,configuration,clock,new DatabentoContractRegistrationRegistry([],options),options);
backend.Runtime.ConfigureDatasetOwnerBackground(date,date,backend.Scope,backend.OptionGateway,backend.Broker,backend.Policy,new("Development"));
var trade=new EstablishedTradeDefinition{Id=new(1,2,701,456),StrategyKind=TradeStrategyKind.IronCondor,Status=EstablishedTradeStatus.Open,
 Legs=new[]{"ES20261120C8050","ES20261120C8100","ES20261120P7325","ES20261120P7375"}.Select(id=>new TradeLegDefinition{ContractId=id}).ToArray()};
var db=Proxy.Create<ITradeDbContext>((method,arguments)=>method.Name=="GetEstablishedTradeAsync"?Task.FromResult<EstablishedTradeDefinition?>(trade):throw new Exception("Unexpected database call: "+method.Name));
var factory=Proxy.Create<IDbContextFactory>((method,arguments)=>method.Name=="get_TradeDb"?db:throw new Exception("Unexpected database port: "+method.Name));
var store=new Store();var recovery=new NativeOwnerTradeRecovery(backend,store,factory,NullLogger<TradeFeedRecoveryService>.Instance);
using var lifetime=new CancellationTokenSource(TimeSpan.FromSeconds(10));
var before=backend.Runtime.ReadDatasetOwnerHealth(MarketDataLane.OptionsGlbx).Revision;
if(await recovery.LoadAsync(trade.Id.Format(),date,false,lifetime.Token))throw new Exception("Absent saved intent enabled a trade.");
using(var cancelled=new CancellationTokenSource()){
 cancelled.Cancel();try{await recovery.LoadAsync(trade.Id.Format(),date,true,cancelled.Token);throw new Exception("Cancelled command was admitted.");}catch(OperationCanceledException){}
}
if(store.Row is not null||backend.Runtime.ReadDatasetOwnerHealth(MarketDataLane.OptionsGlbx).Revision!=before)throw new Exception("Disabled/cancelled commands changed durable or native demand.");
using var caller=new CancellationTokenSource();
var restore=recovery.LoadAsync(trade.Id.Format(),date,true,caller.Token);
while(backend.Runtime.ReadDatasetOwnerHealth(MarketDataLane.OptionsGlbx).Revision<before+4)await Task.Delay(10,lifetime.Token);
if(restore.IsCompleted)throw new Exception("Restoration claimed readiness despite unavailable source dependencies.");
if(store.Row is null||store.Row.OptionContracts.Length!=4)throw new Exception("Complete durable intent was not saved before source readiness.");
caller.Cancel();try{await restore;throw new Exception("Readiness observer ignored caller cancellation.");}catch(OperationCanceledException){}
if(backend.Runtime.ReadDatasetOwnerHealth(MarketDataLane.OptionsGlbx).Revision<before+4||store.Row.OptionContracts.Length!=4)throw new Exception("Cancelled readiness withdrew accepted demand.");
var revision=backend.Runtime.ReadDatasetOwnerHealth(MarketDataLane.OptionsGlbx).Revision;
using(var repeated=new CancellationTokenSource()){
 var same=recovery.LoadAsync(trade.Id.Format(),date,false,repeated.Token);
 await Task.Delay(100,lifetime.Token);
 if(backend.Runtime.ReadDatasetOwnerHealth(MarketDataLane.OptionsGlbx).Revision!=revision)
  throw new Exception("Loading already accepted four-leg intent restarted native dataset setup.");
 if(same.IsCompleted)throw new Exception("Repeated load fabricated readiness.");
 repeated.Cancel();try{await same;throw new Exception("Repeated readiness observer ignored cancellation.");}catch(OperationCanceledException){}
}
if(await recovery.LoadAsync(trade.Id.Format(),date.AddDays(1),false,lifetime.Token))throw new Exception("Wrong ValueDate restored subscriptions.");
if(backend.Runtime.ReadDatasetOwnerHealth(MarketDataLane.OptionsGlbx).Revision!=revision)throw new Exception("Wrong ValueDate changed accepted demand.");
// Replace a recovering owner while its unavailable provider/broker are still
// active. Neither dependency nor the cancelled managed readiness caller may
// prevent native cancellation/join and fresh same-date admission.
var prior=backend.Runtime.ReadDatasetOwnerBackendStatus(true).HostInstanceId;
await backend.Runtime.ReinitializeDatasetOwnerAsync(date).WaitAsync(lifetime.Token);
var fresh=backend.Runtime.ReadDatasetOwnerBackendStatus(true);
if(fresh.HostInstanceId==prior||fresh.Datasets.Any(row=>row.Running||row.ValueDate is not null))
 throw new Exception("Fresh opening retained old owner/publication state.");
backend.Runtime.ConfigureDatasetOwnerBackground(date,date,backend.Scope,backend.OptionGateway,backend.Broker,backend.Policy,new("Development"));
backend.Runtime.StopDatasetOwnerFeeds();await backend.Runtime.WaitDatasetOwnerFeedsStoppedAsync().WaitAsync(lifetime.Token);
prior=fresh.HostInstanceId;
await backend.Runtime.ReinitializeDatasetOwnerAsync(date.AddDays(1)).WaitAsync(lifetime.Token);
fresh=backend.Runtime.ReadDatasetOwnerBackendStatus(true);
if(fresh.HostInstanceId==prior)throw new Exception("Stopped owner was reused for next-day opening.");
backend.Runtime.ConfigureDatasetOwnerBackground(date.AddDays(1),date.AddDays(1),backend.Scope,backend.OptionGateway,backend.Broker,backend.Policy,new("Development"));
try{
 backend.Runtime.ConfigureDatasetOwnerBackground(date,date,backend.Scope,backend.OptionGateway,backend.Broker,backend.Policy,new("Development"));
 throw new Exception("Previous-date configuration was accepted into a fresh epoch.");
}catch(InvalidOperationException){}
using(var cancelled=new CancellationTokenSource()){
 cancelled.Cancel();try{await backend.Runtime.ReinitializeDatasetOwnerAsync(date,cancelled.Token);throw new Exception("Cancelled opening was accepted.");}catch(OperationCanceledException){}
}
if(backend.Runtime.ReadDatasetOwnerBackendStatus(true).HostInstanceId!=fresh.HostInstanceId)throw new Exception("Cancelled opening replaced current epoch.");
backend.Runtime.StopDatasetOwnerFeeds();await backend.Runtime.WaitDatasetOwnerFeedsStoppedAsync().WaitAsync(lifetime.Token);
Console.WriteLine("PASS fresh opening from recovering and stopped states; same-date replacement; next-day rollover; old-date fencing; cancellation before admission; same native runtime and joined retirement.");
Console.WriteLine("PASS four durable leg commands admitted before readiness; unavailable source cannot block later legs; no premature success; cancellation preserves demand; repeated trade load preserves native revision/deadline; API ValueDate isolation; joined native shutdown.");
sealed class Clock(DateTimeOffset at):TimeProvider{public override DateTimeOffset GetUtcNow()=>at;}
sealed class Store:ITradeFeedRecoveryStore{
 public TradeFeedRecoveryRegistration? Row;
 public Task<TradeFeedRecoveryRegistration[]> ReadAsync(DateOnly date,CancellationToken token)=>Task.FromResult(Row is {} row&&row.ValueDate==date?new[]{row}:Array.Empty<TradeFeedRecoveryRegistration>());
 public Task SaveAsync(TradeFeedRecoveryRegistration row,CancellationToken token){Row=row;return Task.CompletedTask;}
 public Task RemoveAsync(string trade,DateOnly date,CancellationToken token){Row=null;return Task.CompletedTask;}
 public Task CloseAsync(DateOnly date,CancellationToken token){Row=null;return Task.CompletedTask;}
}
public class Proxy:DispatchProxy{
 public Func<MethodInfo,object?[]?,object?> Handler=null!;
 public static T Create<T>(Func<MethodInfo,object?[]?,object?> handler)where T:class{var proxy=DispatchProxy.Create<T,Proxy>();((Proxy)(object)proxy).Handler=handler;return proxy;}
 protected override object? Invoke(MethodInfo? method,object?[]? args)=>Handler(method!,args);
}
