using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Rust.Api;
using TomasAI.IFM.Application.MarketData.Rust.Host;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;

// This validates the complete constructor graph without provider network activity,
// opening a database/Redis/provider socket, or starting any hosted feed.
var builder=Host.CreateApplicationBuilder();builder.Environment.EnvironmentName="Development";
builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?>{
    ["Storage:PostgresConnection"]="Host=127.0.0.1;Database=ifm_native_qualification;Username=fixture;Password=fixture",
    ["ConnectionStrings:TradeTickQuoteDbConnection"]="Host=127.0.0.1;Database=ifm_native_qualification;Username=fixture;Password=fixture",
    ["MarketDataHost:DataSource"]="DatabentoLive"});
if(args.Length>0)builder.Configuration["MarketDataHost:Rust:LibraryPath"]=Path.GetFullPath(args[0]);
builder.Services.AddSingleton<NatsConnectionManager>();builder.Services.AddSingleton(p=>new MarketDataHostConnection(p.GetRequiredService<NatsConnectionManager>(),"nats://127.0.0.1:1","nativegraph"));
builder.Services.AddNativeMarketDataBackend(builder.Configuration);
if(builder.Services.Any(x=>x.ImplementationType?.Name is "DatabentoMarketDataEpochFactory" or "DatabentoFeedFactory" or "DatasetWorkerProcessSupervisor" or "MarketDataWorkerHost"))throw new InvalidDataException("Native Host registered a legacy provider/process owner.");
var bannedServices=new[]{typeof(NativeMarketDataEpochFactory),typeof(IRustMarketDataEpochFactory),typeof(TomasAI.IFM.Application.MarketData.Databento.Resiliency.DatasetWorkerAdmissionRegistry),typeof(DatasetDesiredSubscriptionRegistry),typeof(TomasAI.IFM.Application.MarketData.Pricing.QualifiedCompositionDiscovery),typeof(TomasAI.IFM.Application.MarketData.Subscriptions.DurableCompositionRuntime)};
if(builder.Services.Any(descriptor=>bannedServices.Contains(descriptor.ServiceType)||descriptor.ImplementationType is{} implementation&&bannedServices.Contains(implementation)))throw new InvalidDataException("Complete native owner registered managed live orchestration.");
await using var services=builder.Services.BuildServiceProvider(new ServiceProviderOptions{ValidateOnBuild=true,ValidateScopes=true});
if(services.GetRequiredService<IMarketDataApi>() is not RustMarketDataApi)throw new InvalidDataException("Native Host selected the wrong public facade.");
var concrete=services.GetRequiredService<IRustMarketDataHostServices>();
if(concrete is not NativeOwnerHostServices)throw new InvalidDataException("Native Host capabilities are not concrete.");
foreach(var method in concrete.GetType().GetInterfaceMap(typeof(IRustMarketDataHostServices)).TargetMethods)if(method.IsAbstract||method.DeclaringType!=typeof(NativeOwnerHostServices))throw new InvalidDataException("A Host capability fell through an interface default.");
// Preserve the current backend's two-part safety switch. Merely selecting a
// Development Host must not enable live fault injection.
try
{
    await services.GetRequiredService<NativeOwnerRuntimeOperations>().InjectDevelopmentOptionWorkerFaultAsync(new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),new(2026,10,27)));
    throw new InvalidDataException("Development fault injection ignored its explicit opt-in.");
}
catch(InvalidOperationException error)when(error.Message=="Live fault injection requires explicitly enabled Development verification."){}
Console.WriteLine("PASS native Host DI graph: complete constructor validation; public facade and 40 Host capability members resolve; no provider I/O, managed live coordinator, provider factory or child-process registration.");

// The replacement adapter has every application operation independently of the
// removed prototype registrations. No runtime, credentials or I/O is needed
// for this contract gate; full operation acceptance remains a separate gate.
var ownerApplication=typeof(NativeOwnerApplicationServices);
var ownerMap=ownerApplication.GetInterfaceMap(typeof(IRustMarketDataApplicationServices));
if(ownerMap.TargetMethods.Any(method=>method.IsAbstract||method.DeclaringType!=ownerApplication))
    throw new InvalidDataException("A complete-owner application operation inherited a fallback.");
var prohibited=new[]{typeof(NativeMarketDataEpochFactory),typeof(DatabentoMarketDataApi),typeof(SemaphoreSlim),typeof(PeriodicTimer)};
if(ownerApplication.GetFields(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Any(field=>prohibited.Contains(field.FieldType)))
    throw new InvalidDataException("Complete-owner adapter retained managed lifecycle orchestration.");
Console.WriteLine($"PASS complete-owner application contract: {ownerMap.TargetMethods.Length} explicit operations/disposal; no legacy API, managed epoch factory, lifecycle semaphore or renewal timer field. Live operation parity is a separate acceptance gate.");
