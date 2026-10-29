using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Application.MarketData.Rust.Api;
using TomasAI.IFM.Application.MarketData.Rust.Host;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.TradeDb.Schema;
using TomasAI.IFM.Domain.Trade.Shared.Recovery;
using TomasAI.IFM.Shared.Storage;

// Own one ephemeral PostgreSQL container only. No application/test database,
// production credential, schema or currently running service is modified.
var name="ifm-native-store-"+Guid.NewGuid().ToString("N");
var previous=new Dictionary<string,string?>();
void Set(string key,string value){previous[key]=Environment.GetEnvironmentVariable(key);Environment.SetEnvironmentVariable(key,value);}
Set("DOTNET_ENVIRONMENT","Test");Set("ASPNETCORE_ENVIRONMENT","Test");Set("POSTGRES_TEST_KEY",JsonSerializer.Serialize(new{userid="postgres",password="native-fixture-only"}));
var started=false;
try
{
    await Docker("run","--detach","--rm","--name",name,"--label","ifm.native.fixture="+name,"--publish","127.0.0.1::5432","--env","POSTGRES_PASSWORD=native-fixture-only","--env","POSTGRES_DB=ifm_native_fixture","postgres:17.2");started=true;
    var endpoint=(await Docker("port",name,"5432/tcp")).Trim();var port=int.Parse(endpoint[(endpoint.LastIndexOf(':')+1)..]);
    var connection=$"Host=127.0.0.1;Port={port};Database=ifm_native_fixture";
    using var deadline=new CancellationTokenSource(TimeSpan.FromMinutes(2));
    await using(var admin=new NpgsqlConnection(connection+";Username=postgres;Password=native-fixture-only;Timeout=2"))
    {
        while(true){try{await admin.OpenAsync(deadline.Token);break;}catch(NpgsqlException){await Task.Delay(100,deadline.Token);}}
        await using var command=new NpgsqlCommand(TradeSchemaSql.LiveFeedRecoveryCreate,admin);await command.ExecuteNonQueryAsync(deadline.Token);
    }
    var settings=new DbConnectionSettings().Add(TradeDbContext.TradeDbConnection,connection,"System.Data.Postgres");
    var store=new TradeFeedRecoveryStore(settings);var date=new DateOnly(2026,9,8);var next=date.AddDays(1);
    await store.SaveAsync(new("1.1.1901.1501",date,["ES-option-0","ES-option-1","ES-option-2","ES-option-3"],DateTimeOffset.UtcNow),deadline.Token);
    await store.SaveAsync(new("1.1.1901.1502",next,["next-date-option"],DateTimeOffset.UtcNow),deadline.Token);
    await using var services=new ServiceCollection().AddSingleton<ITradeFeedRecoveryStore>(store).BuildServiceProvider();
    var epochs=new NativeMarketDataEpochFactory(TimeProvider.System,(_,_,_)=>throw new InvalidOperationException("Read intent must not start a native epoch."));
    var host=new NativeHostApplicationServices(services,epochs,TimeProvider.System);
    var saved=await host.ReadTradeFeedRecoveryAsync(date,deadline.Token);
    if(saved.Length!=1||saved[0].OptionContracts.Length!=4||!host.EnabledTrades(date).SequenceEqual(["1.1.1901.1501"]))throw new InvalidDataException("Native Host recovery intent read lost date/contracts/health ownership.");
    await store.SaveAsync(saved[0] with{OptionContracts=["replacement-contract"]},deadline.Token);
    var replaced=await host.ReadTradeFeedRecoveryAsync(date,deadline.Token);
    if(!replaced.Single().OptionContracts.SequenceEqual(["replacement-contract"]))throw new InvalidDataException("Intent update appended stale contracts.");
    await store.RemoveAsync("1.1.1901.1501",date,deadline.Token);
    if((await host.ReadTradeFeedRecoveryAsync(date,deadline.Token)).Length!=0||host.EnabledTrades(date).Length!=0)throw new InvalidDataException("Disabled intent remained available for recovery.");
    if((await host.ReadTradeFeedRecoveryAsync(next,deadline.Token)).Single().TradeId!="1.1.1901.1502")throw new InvalidDataException("A different ValueDate was deleted.");
    await store.CloseAsync(date,deadline.Token);
    if((await store.ReadAsync(next,deadline.Token)).Length!=1)throw new InvalidDataException("Market-close cleanup erased a future date.");
    await store.CloseAsync(next,deadline.Token);
    if((await host.ReadTradeFeedRecoveryAsync(next,deadline.Token)).Length!=0)throw new InvalidDataException("Market-close cleanup retained intent.");
    Console.WriteLine("PASS native Host and real PostgreSQL recovery intent: explicit date/four contracts; atomic replacement; disabled intent removed from health; dates isolated; market close cleanup; no native epoch/provider started.");
}
finally
{
    if(started)await Docker("rm","--force",name);
    foreach(var item in previous)Environment.SetEnvironmentVariable(item.Key,item.Value);
}
static async Task<string> Docker(params string[] arguments)
{
    var start=new ProcessStartInfo("docker"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
    foreach(var value in arguments)start.ArgumentList.Add(value);
    using var process=Process.Start(start)??throw new InvalidOperationException("Docker did not start.");var output=process.StandardOutput.ReadToEndAsync();var errors=process.StandardError.ReadToEndAsync();
    using var deadline=new CancellationTokenSource(TimeSpan.FromMinutes(2));await process.WaitForExitAsync(deadline.Token);
    var result=await output;var failure=await errors;if(process.ExitCode!=0)throw new InvalidOperationException("Isolated PostgreSQL fixture failed: "+failure);return result;
}
