using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Recovery;
using TomasAI.IFM.Application.MarketData.Rust.Api;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Application.MarketData.Rust.Host;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Recovery;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.ReferenceData;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using TomasAI.IFM.Shared.MarketData.Definitions;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using var input=JsonDocument.Parse(await File.ReadAllBytesAsync(args[1]));var config=input.RootElement;
var at=DateTimeOffset.UnixEpoch.AddTicks(config.GetProperty("at").GetInt64());var date=DateOnly.FromDayNumber(config.GetProperty("date").GetInt32());
Environment.SetEnvironmentVariable("DATABENTO_API_KEY",new string('x',32)); // qualification child only; never a real credential
var configuration=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
 ["MarketDataHost:Rust:LibraryPath"]=Path.GetFullPath(args[0]),["MarketDataHost:Rust:AdvancingClock"]="false",
 ["MarketDataHost:Rust:TreasuryEndpoint"]=config.GetProperty("treasury").GetString(),["MarketDataHost:NatsUrl"]=config.GetProperty("broker").GetString(),
 ["MarketDataHost:Scope"]="development",["MarketDataHost:Rust:GlbxHost"]="127.0.0.1",["MarketDataHost:Rust:GlbxPort"]=config.GetProperty("port").ToString(),
 ["MarketDataHost:Rust:DnsServers:0"]="127.0.0.1:1"}).Build();
var clock=new Clock(at);await using var cold=new NativeColdBackend(configuration,clock,NullLogger<NativeColdBackend>.Instance);
var options=new DatabentoMarketDataRuntimeOptions{Contracts=[],FeedOptions=DatabentoFeedOptions.ForProfile(FeedDeploymentProfile.Development,"GLBX.MDP3")};
var registrations=new DatabentoContractRegistrationRegistry([],options);
await using var backend=new NativeDatasetBackend(cold,configuration,clock,registrations,options);
// All backing database/native-reference jobs are in the independent fixture.
// These poison ports prove the selected Host browser does no managed acquisition.
var databases=Proxy.Create<IDbContextFactory>((method,arguments)=>throw new Exception("Unexpected local browser database operation: "+method.Name));
var tradeStore=Proxy.Create<ITradeFeedRecoveryStore>((method,arguments)=>throw new Exception("Browser unexpectedly used trade recovery persistence."));
var trades=new NativeOwnerTradeRecovery(backend,tradeStore,databases,NullLogger<TradeFeedRecoveryService>.Instance);
await using var services=new ServiceCollection().AddSingleton(new NativeOwnedOptionChainCache(backend.Runtime)).BuildServiceProvider();
var business=new NativeOwnerBusinessOperations(services,backend,trades,clock);
var host=new NativeOwnerHostServices(business,new NativeOwnerRuntimeOperations(backend,trades,services),new());
var application=Proxy.Create<IRustMarketDataApplicationServices>((method,arguments)=>method.Name=="DisposeAsync"?ValueTask.CompletedTask:throw new Exception("Browser fell through an unrelated application operation: "+method.Name));
await using var api=new RustMarketDataApi(application,host);
backend.Runtime.ConfigureDatasetOwnerBackground(date,date,backend.Scope,backend.OptionGateway,backend.Broker,backend.Policy,new("Development"));
await backend.Runtime.WaitDatasetOwnerReadyAsync(MarketDataLane.OptionsGlbx,false).WaitAsync(TimeSpan.FromSeconds(8));
var raw=config.GetProperty("model");
var query=new GetEvaluatedOptionChainQuery{UnderlyingContractId=raw[33].GetString()!,UnderlyingSymbol="ES",ExpiryDate=date.AddDays(14),RequiredContractIds=[raw[0].GetString()!],SubscriptionOwnerId="view-one",SpreadWingWidth=25};
var result=await api.EvaluateOptionChainAsync(query).WaitAsync(TimeSpan.FromSeconds(8));
var model=result.Success?result.Value:throw new Exception("Native browser failed: "+result.ErrorMessage);
var row=model?.Contracts.Single()??throw new Exception("Native browser returned no complete contract.");
if(row.ContractId!=raw[0].GetString()||row.Strike!=6000||!row.IsCall||!row.GreeksValid||row.ImpliedVolatility is not >0||row.Bid is not >0||row.Ask<row.Bid||row.VolumeValueDate!=date)
 throw new Exception("Actual C# browser DTO materialization lost quote/Greek/statistics fields.");
var before=backend.Runtime.ReadDatasetOwnerBackendStatus(true).Datasets.Single(dataset=>dataset.Role=="Options");
query.SubscriptionOwnerId="view-two";
var second=await api.EvaluateOptionChainAsync(query).WaitAsync(TimeSpan.FromSeconds(8));
if(!second.Success||second.Value?.WindowMethod!="ImpliedVolatility5Delta"||backend.Runtime.ReadDatasetOwnerIdentity(MarketDataLane.OptionsGlbx)?.Generation!=before.GenerationId)throw new Exception("Native ATM window or shared-view source continuity failed.");
var released=await api.EvaluateOptionChainAsync(query with{SubscriptionOwnerId="view-one",ReleaseOnly=true}).WaitAsync(TimeSpan.FromSeconds(8));
if(!released.Success||released.Value?.WindowMethod!="Released"||backend.Runtime.ReadDatasetOwnerBinding(row.ContractId,true) is not{Active:true,Healthy:true})throw new Exception("Releasing one view removed another native owner.");
var recovered=await backend.Runtime.RecoverDatasetOwnerAsync(new(Guid.NewGuid(),"Options","GLBX.MDP3",before.WorkerInstanceId,before.GenerationId,date,"Actual browser adapter recovery qualification")).WaitAsync(TimeSpan.FromSeconds(8));
if(!recovered.Recovered||!recovered.CleanupConfirmed||recovered.GenerationId==before.GenerationId)throw new Exception("Native browser dataset recovery failed.");
var restored=await api.EvaluateOptionChainAsync(query).WaitAsync(TimeSpan.FromSeconds(8));
if(!restored.Success||restored.Value?.Contracts.Single().ContractId!=row.ContractId||backend.Runtime.ReadDatasetOwnerBinding(row.ContractId,true)?.Generation!=recovered.GenerationId)throw new Exception("Native view demand or result did not survive physical replacement.");
await api.EvaluateOptionChainAsync(query with{ReleaseOnly=true}).WaitAsync(TimeSpan.FromSeconds(8));
if(backend.Runtime.ReadDatasetOwnerBinding(row.ContractId,true) is{Active:true})throw new Exception("Final native view did not retire its provider source.");
backend.Runtime.StopDatasetOwnerFeeds();await backend.Runtime.WaitDatasetOwnerFeedsStoppedAsync().WaitAsync(TimeSpan.FromSeconds(8));
Console.WriteLine("PASS actual C# -> DLL -> native-owned SDK browser: published IMarketDataApi/Host adapter; typed quote/Greek/statistics values; native ATM window; two-view continuity; release isolation; physical recovery with retained managed results; restored view demand; final release and joined shutdown; no managed acquisition ports.");
sealed class Clock(DateTimeOffset at):TimeProvider{public override DateTimeOffset GetUtcNow()=>at;}
public class Proxy:DispatchProxy{
 public Func<MethodInfo,object?[]?,object?> Handler=null!;
 public static T Create<T>(Func<MethodInfo,object?[]?,object?> handler)where T:class{var proxy=DispatchProxy.Create<T,Proxy>();((Proxy)(object)proxy).Handler=handler;return proxy;}
 protected override object? Invoke(MethodInfo? method,object?[]? args)=>Handler(method!,args);
}
