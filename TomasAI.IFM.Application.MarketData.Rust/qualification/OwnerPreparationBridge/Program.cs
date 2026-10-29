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
var calendar=new OptionPricingCalendar("fixture/v1","Eastern Standard Time",date.AddDays(-1),date.AddDays(30),new(18,0),Enumerable.Range(-1,32).Select(date.AddDays).ToImmutableArray());
var publication=new TreasuryPublicationPolicy("fixture/v1",at.AddDays(-1),at.AddDays(1),[new(date.AddDays(-1),at.AddDays(-1))],"Eastern Standard Time");
var raw=config.GetProperty("model");var definition=new OptionContractDefinition{Dataset=raw[14].GetString()!,RawSymbol=raw[17].GetString()!,Ticker=raw[2].GetString()!,Underlying=raw[33].GetString()!,
 Instrument=new(raw[15].GetUInt16(),raw[16].GetUInt32()),Right=(OptionRightSelection)raw[35].GetByte(),StrikePrice=decimal.Parse(raw[11].GetString()!,System.Globalization.CultureInfo.InvariantCulture),
 MaturityDate=date.AddDays(14),ExpirationTimestampNanoseconds=checked((ulong)raw[21][0].GetInt64()*100)};
var candidates=new[]{new OptionDefinitionCandidate(raw[0].GetString()!,raw[29].GetString()!,raw[19].GetString()!,definition)};
var key=new CompositionPreparationKey(Guid.NewGuid(),1,new('a',64));var owner=NativePreparationIdentity.For(key);
if(owner==Guid.Empty||owner!=NativePreparationIdentity.For(key)||owner==NativePreparationIdentity.For(key with{InputRevision=2}))throw new Exception("Unstable preparation business identity.");
// Establish the same accepted command before invoking the business operation;
// this fixture intentionally has no primary-futures feed or startup DB process.
backend.Runtime.ConfigureDatasetOwnerDiscovery(new(owner,Guid.Empty,date,date.AddDays(14),at.AddMinutes(2),candidates,true,calendar,publication,UsTreasuryCurve.ConversionPolicy),
 backend.Scope,backend.OptionGateway,backend.Broker,backend.Policy,new(key.WorkflowId.ToString("D"),key.InputRevision,key.InputSha256));
await backend.Runtime.WaitDatasetOwnerReadyAsync(MarketDataLane.OptionsGlbx,true).WaitAsync(TimeSpan.FromSeconds(8));
var model=new FuturesContractV3ReadModel{ContractId=definition.Underlying,Symbol="ES",LocalSymbol="ESZ6",SecurityType="FUT",Currency="USD",Exchange="XCME",Multiplier="50",LastTradeDate=new(2026,12,18)};
var db=Proxy.Create<ISecuritiesDbContext>((m,a)=>m.Name=="GetFuturesContractAsync"?Task.FromResult<FuturesContractV3ReadModel?>(model):throw new Exception("Unexpected metadata read: "+m.Name));
var factory=Proxy.Create<IDbContextFactory>((m,a)=>m.Name=="get_SecuritiesDb"?db:throw new Exception("Unexpected database read: "+m.Name));
var tradeStore=Proxy.Create<ITradeFeedRecoveryStore>((m,a)=>throw new Exception("Preparation unexpectedly touched trade live-feed persistence."));
var trades=new NativeOwnerTradeRecovery(backend,tradeStore,factory,NullLogger<TradeFeedRecoveryService>.Instance);
var preparations=new Store();var plans=new Plans();
var services=new ServiceCollection().AddSingleton<ICompositionPreparationStore>(preparations).AddSingleton<ICompositionRoutePlanStore>(plans)
 .AddSingleton(new NativeCompositionPlanPersistence(backend,factory,plans)).AddSingleton(new NativeOwnedOptionChainCache(backend.Runtime)).BuildServiceProvider();
await using var lifetime=services;
var business=new NativeOwnerBusinessOperations(services,backend,trades,clock);
var plan=new CompositionMarketDataPlan{ValueDate=date,IncludeOptions=true,ScopeComplete=true,MaturityDate=date.AddDays(14),Options=candidates.ToImmutableArray(),Calendar=calendar,Publication=publication,Conversion=UsTreasuryCurve.ConversionPolicy};
var preparing=business.PrepareCompositionAsync(key,plan,"Daily",at.AddMinutes(2));
await preparations.Entered.Task.WaitAsync(TimeSpan.FromSeconds(8));
if(preparing.IsCompleted||plans.Saved.Count!=1)throw new Exception("The copied business commit was not actually stalled.");
var before=backend.Runtime.ReadDatasetOwnerBackendStatus(true).Datasets.Single(row=>row.Role=="Options");
var recovered=await backend.Runtime.RecoverDatasetOwnerAsync(new(Guid.NewGuid(),"Options","GLBX.MDP3",before.WorkerInstanceId,before.GenerationId,date,"Stalled business commit qualification")).WaitAsync(TimeSpan.FromSeconds(8));
if(!recovered.Recovered||!recovered.CleanupConfirmed||recovered.GenerationId==before.GenerationId||preparing.IsCompleted)throw new Exception("A stalled C# business commit pinned native dataset recovery.");
preparations.Release.TrySetResult();var prepared=await preparing.WaitAsync(TimeSpan.FromSeconds(8));
if(prepared.Preparation is not{} evidence||prepared.Failure is not null||evidence.Snapshot.Instruments.Length!=1||evidence.DiscoveryLease is null
 ||!plans.Saved.ContainsKey(evidence.Request.ScopeId)||evidence.DiscoveryLease.ScopeId!=evidence.Request.ScopeId)throw new Exception("Native business preparation did not seal the existing public contract.");
CompositionPreparationService.Validate(evidence);
var replay=await business.PrepareCompositionAsync(key,plan,"Daily",at.AddSeconds(30));
if(replay.Preparation!=evidence||preparations.Commits!=1)throw new Exception("Retry recaptured or recommitted immutable financial evidence.");
var restored=backend.Runtime.ReadDatasetOwnerDiscovery(owner).Lease??throw new Exception("Native intent was lost during stalled persistence.");
if(restored.GenerationId!=recovered.GenerationId||restored.ScopeId!=evidence.Request.ScopeId||restored.Options[0].Pricing.GenerationId!=recovered.GenerationId)throw new Exception("Business alias or native contexts were not reconstructed.");
// An authoritative empty universe freezes empty business evidence without
// manufacturing a physical options generation or acquiring another SDK source.
var emptyKey=key with{WorkflowId=Guid.NewGuid()};
var empty=await business.PrepareCompositionAsync(emptyKey,plan with{Options=[]},"Daily",at.AddMinutes(2)).WaitAsync(TimeSpan.FromSeconds(8));
if(empty.Preparation is not{} emptyEvidence||empty.Failure is not null||!emptyEvidence.Snapshot.Instruments.IsEmpty||emptyEvidence.DiscoveryLease is not null
 ||emptyEvidence.Snapshot.GenerationId!=backend.Runtime.ReadDatasetOwnerBackendStatus(true).HostInstanceId)throw new Exception("Complete-empty evidence manufactured a physical source or lost its native Host business fence.");
CompositionPreparationService.Validate(emptyEvidence);
var emptyReplay=await business.PrepareCompositionAsync(emptyKey,plan with{Options=[]},"Daily",at.AddMinutes(2));
if(emptyReplay.Preparation!=emptyEvidence||preparations.Commits!=2)throw new Exception("Complete-empty retry recaptured immutable evidence.");
var released=await business.ReleaseCompositionAsync("GLBX.MDP3",new(evidence.DiscoveryLease.ScopeId,evidence.DiscoveryLease.LeaseId,evidence.DiscoveryLease.GenerationId)).WaitAsync(TimeSpan.FromSeconds(8));
if(released.Active||released.Failure is not null)throw new Exception("Logical release across physical replacement failed.");
var repeated=await business.ReleaseOptionScopeAsync(restored).WaitAsync(TimeSpan.FromSeconds(8));
if(repeated.Active||repeated.Failure is not null)throw new Exception("Repeated logical release was not idempotent.");
backend.Runtime.StopDatasetOwnerFeeds();await backend.Runtime.WaitDatasetOwnerFeedsStoppedAsync().WaitAsync(TimeSpan.FromSeconds(8));
Console.WriteLine("PASS actual C# -> DLL -> native-owned SDK preparation: sealed reconstruction alias; coherent snapshot; blocked C# commit cannot pin native cancellation/join/rebuild; immutable retry; fresh recovered contexts; explicit release; authoritative complete-empty evidence and immutable replay; joined shutdown.");
sealed class Clock(DateTimeOffset at):TimeProvider{public override DateTimeOffset GetUtcNow()=>at;}
sealed class Store:ICompositionPreparationStore{
 public readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
 readonly ConcurrentDictionary<CompositionPreparationKey,CompositionPreparation> values=new();public int Commits;
 public Task<CompositionPreparation?> ReadAsync(CompositionPreparationKey key,CancellationToken token){token.ThrowIfCancellationRequested();return Task.FromResult(values.GetValueOrDefault(key));}
 public async Task<CompositionPreparation> CommitAsync(CompositionPreparation value,CancellationToken token){Entered.TrySetResult();await Release.Task.WaitAsync(token);Interlocked.Increment(ref Commits);return values.GetOrAdd(value.Key,value);}
}
sealed class Plans:ICompositionRoutePlanStore{
 public readonly ConcurrentDictionary<string,CompositionRoutePlan> Saved=new();
 public Task SaveAsync(CompositionRoutePlan plan,CancellationToken token){token.ThrowIfCancellationRequested();plan.Validate();Saved.TryAdd(plan.PlanId,plan);return Task.CompletedTask;}
 public Task<CompositionRoutePlan?> ReadAsync(string id,CancellationToken token)=>Task.FromResult(Saved.GetValueOrDefault(id));
}
public class Proxy:DispatchProxy{
 public Func<MethodInfo,object?[]?,object?> Handler=null!;
 public static T Create<T>(Func<MethodInfo,object?[]?,object?> handler)where T:class{var proxy=DispatchProxy.Create<T,Proxy>();((Proxy)(object)proxy).Handler=handler;return proxy;}
 protected override object? Invoke(MethodInfo? method,object?[]? args)=>Handler(method!,args);
}
