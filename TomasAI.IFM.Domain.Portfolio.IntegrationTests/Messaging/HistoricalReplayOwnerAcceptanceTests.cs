using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TomasAI.IFM.Application.Api.Server;
using TomasAI.IFM.Application.Api.Server.Core.DependencyInjection;
using TomasAI.IFM.Application.Api.Server.Core.Hosting.Modes;
using TomasAI.IFM.Application.Api.Server.Core.Startup.Schema;
using TomasAI.IFM.Framework.TradeBroker.Contracts;
using TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;
using Xunit;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Messaging;

/// <summary>Finite real-owner fixture, deliberately separate from complete-session qualification.</summary>
[Collection("PortfolioFinancialDatabase")]
public sealed class HistoricalReplayOwnerAcceptanceTests
{
    [PortfolioEnvironmentFact("IFM_RUN_HISTORICAL_OWNER_ACCEPTANCE")]
    [Trait("Category","HistoricalReplayOwnerComponent")]
    public async Task Dedicated_historical_host_uses_real_restored_owners_for_reservation_duplicates_composition_and_risk()
    {
        var apiDll=Environment.GetEnvironmentVariable("IFM_REPLAY_OWNER_API_DLL")??throw new InvalidOperationException("Explicit built API executable path required for component acceptance.");
        if(!Path.IsPathFullyQualified(apiDll)||!File.Exists(apiDll)||!File.Exists(Path.Combine(Path.GetDirectoryName(apiDll)!,"ifm-deployment-manifest.json")))throw new InvalidOperationException("Built actual API deployment artifact is incomplete.");
        var run=Guid.NewGuid().ToString("N")[..12];var prefix="ifm_session_"+run;var network=prefix;
        var targetDatabase=prefix+"_historical_host";
        var root=Path.Combine(Path.GetTempPath(),prefix+"_owner_acceptance");Directory.CreateDirectory(root);
        var postgres=prefix+"_postgres";var scylla=prefix+"_scylla";var nats=prefix+"_nats";var containers=new List<string>();
        var saved=new Dictionary<string,string?>();Process? host=null;Task<string>? stdout=null,stderr=null;var networkCreated=false;var workingDirectory=Directory.GetCurrentDirectory();
        void Env(string key,string? value){saved.TryAdd(key,Environment.GetEnvironmentVariable(key));Environment.SetEnvironmentVariable(key,value);}
        try
        {
            Directory.SetCurrentDirectory(root);
            await Run("docker",["network","create","--label","ifm.session-qualification="+run,network]);
            networkCreated=true;
            foreach(var (name,image,port,extra) in new[]{
                (postgres,"postgres:17.2","5432",new[]{"-e","POSTGRES_PASSWORD=ifm-qualification-only","-e","POSTGRES_DB=fixture_source"}),
                (scylla,"scylladb/scylla:6.2.2","9042",Array.Empty<string>()),
                (nats,"nats:2.12.0-alpine","4222",Array.Empty<string>())})
            {
                var args=new List<string>{"run","-d","--name",name,"--network",network,"--label","ifm.session-qualification="+run,"-p","127.0.0.1::"+port};args.AddRange(extra);args.Add(image);
                if(name==scylla)args.AddRange(["--smp","1","--memory","1G","--overprovisioned","1","--developer-mode","1"]);
                if(name==nats)args.Add("-js");await Run("docker",args);containers.Add(name);
            }
            await Wait(async()=>{await Run("docker",["exec",postgres,"pg_isready","-U","postgres"]);},TimeSpan.FromSeconds(60));
            await Wait(async()=>{await Run("docker",["exec",scylla,"cqlsh","-e","SELECT release_version FROM system.local;"]);},TimeSpan.FromSeconds(150));
            var pgPort=await Port(postgres,"5432");var cqlPort=await Port(scylla,"9042");var natsPort=await Port(nats,"4222");
            var keyspace="fixture_"+run;await Run("docker",["exec",scylla,"cqlsh","-e","CREATE KEYSPACE "+keyspace+" WITH replication={'class':'SimpleStrategy','replication_factor':1};"]);
            var broker="nats://127.0.0.1:"+natsPort;
            var settings=new Dictionary<string,string?>(StringComparer.OrdinalIgnoreCase);
            var sourcePg=$"Host=127.0.0.1;Port={pgPort};Database=fixture_source";
            var targetPg=$"Host=127.0.0.1;Port={pgPort};Database={targetDatabase}";
            var raw=$"Contact Points=127.0.0.1;Port={cqlPort};Default Keyspace={keyspace}";
            foreach(var key in new[]{"EventSourceActor","Configuration","MarketDataService","SystemAdmin","SequenceId","Portfolio","MarketData","Reference","Securities","Trade","TradePlan","ScheduledTask"})settings["ConnectionStrings:"+key+"DbConnection"]=sourcePg;
            settings["ConnectionStrings:TradeTickQuoteDbConnection"]=raw;settings["Storage:PostgresConnection"]=sourcePg;settings["IFM_TEST_POSTGRES_CONNECTION"]=sourcePg;
            foreach(var key in HistoricalSessionReplay.OwnerPathKeys){var path=Path.Combine(root,"owners",key.Replace(':','_'));if(key=="TradeBroker:Emulator:LedgerPath")path=Path.Combine(path,"ledger.json");else Directory.CreateDirectory(path);settings[key]=path;}
            settings["IFM_TEST_NATS_URL"]=broker;settings["Nats:Consumer:Url"]=broker;settings["Nats:JetStreamConsumer:Url"]=broker;settings["MarketDataHost:NatsUrl"]=broker;
            settings["MarketDataHost:Enabled"]="true";settings["AppSettings:Fmp:Enabled"]="false";settings["Telemetry:Metrics:Enabled"]="false";settings["Telemetry:Logs:Enabled"]="false";
            settings["SessionCapture:OwnerCheckpoints:Enabled"]="true";settings["Actors:RealtimeMailboxes:Enabled"]="false";settings["Actors:SessionCapture:Enabled"]="false";settings["AppSettings:IntrinsicTimeStrategyWorkflow:Enabled"]="false";
            settings["ApplicationStartup:AutoStartAfterBootstrap"]="false";settings["RawFuturesStorage:Enabled"]="false";
            settings["AppSettings:HistoricalAnalyticsWarmup:Enabled"]="false";settings["TradeBroker:Emulator:AccountAlias"]="REPLAY-FIXTURE-"+run;settings["TradeBroker:Emulator:OfflineSimulation:Enabled"]="false";
            settings["TradeBroker:Emulator:StartingCash"]="10000";
            settings["StartupOrchestration:MaximumSchemaInitializationConcurrency"]="1";
            settings["ScheduledTasks:OutputRoot"]=Path.Combine(root,"owners","scheduled-output");settings["DatabaseBackup:OutputRoot"]=Path.Combine(root,"owners","backup-output");
            Env("DATABENTO_API_KEY",null);Env("FMP_API_KEY",null);Env("IFM_TEST_ACTOR_DOMAIN",null);Env("POSTGRES_TEST_KEY","{\"userid\":\"postgres\",\"password\":\"ifm-qualification-only\"}");Env("SCYLLADB_TEST_KEY","{\"userid\":\"cassandra\",\"password\":\"cassandra\"}");
            Env("DOTNET_ENVIRONMENT","Test");Env("ASPNETCORE_ENVIRONMENT","Test");
            // Actual deployment initializer only; no actor, source or HTTP server starts during fixture setup.
            var builder=WebApplication.CreateBuilder(new WebApplicationOptions{EnvironmentName="Test",ContentRootPath=Path.GetDirectoryName(apiDll)});
            builder.Configuration.AddInMemoryCollection(settings);CoreServiceRegistration.ConfigureApiServer(builder,out var logger);
            CoreServiceRegistration.RegisterServices(builder.Services,builder.Configuration,logger,builder.Environment);
            builder.Services.RemoveAll<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();builder.Services.AddDistributedMemoryCache();
            await using(var setup=builder.Build())
            {
                CoreServiceRegistration.ConfigureRequestPipeline(setup,logger);
                await setup.Services.GetRequiredService<ApplicationSchemaInitializer>().InitializeAsync();
                var ownerContainer=setup.Services.GetRequiredService<SimpleInjector.Container>();
                var context=ownerContainer.GetInstance<TomasAI.IFM.Shared.EventModelActor.Contracts.IRealtimeActorContext<TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Realtime.Actor.IronCondorTradePositionRealtimeActor>>();
                var recorderField=context.GetType().GetField("sessionRecorder",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
                Assert.Same(setup.Services.GetRequiredService<TomasAI.IFM.Shared.EventModelActor.Contracts.IRealtimeSessionRecorder>(),recorderField.GetValue(context));
                var engine=ownerContainer.GetInstance<EmulatorLedger>();
                var receipt=engine.Cancel(new(settings["TradeBroker:Emulator:AccountAlias"]!,"fixture-no-such-order",Guid.NewGuid(),1));
                Assert.Equal(FrameworkDispatchOutcome.RejectedLocally,receipt.Outcome);Assert.Equal(10000m,engine.Snapshot().CashBalance);
                var ledgerStore=ownerContainer.GetInstance<IEmulatorLedgerStore>();
                await ownerContainer.DisposeContainerAsync();
                // RegisterInstance is externally owned; the fixture created this object and must close its file lease.
                (ledgerStore as IDisposable)?.Dispose();
            }
            var originalLedger=settings["TradeBroker:Emulator:LedgerPath"]!;
            var restoredLedger=Path.Combine(root,"owners","restored-emulator","ledger.json");Directory.CreateDirectory(Path.GetDirectoryName(restoredLedger)!);File.Copy(originalLedger,restoredLedger);
            Assert.Equal(Hash(originalLedger),Hash(restoredLedger));settings["TradeBroker:Emulator:LedgerPath"]=restoredLedger;
            var outbox=typeof(TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes.DurableRealtimeMailboxHub).Assembly.GetType("TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes.RealtimeSourceOutbox",true)!;
            await using(var normalJournal=(IAsyncDisposable)Activator.CreateInstance(outbox,[new TomasAI.IFM.Shared.EventModelActor.RealtimeMailboxOptions{Directory=settings["Actors:RealtimeMailboxes:Directory"]!,Epoch="owner_fixture"},false])!){ }
            // Actual PostgreSQL custom dump/restore; raw CQL is empty and its actual schema is re-created.
            var dump=await RunBytes("docker",["exec",postgres,"pg_dump","-U","postgres","-Fc","fixture_source"]);
            await Run("docker",["exec",postgres,"psql","-U","postgres","-c","CREATE DATABASE "+targetDatabase+";"]);
            await RunBytes("docker",["exec","-i",postgres,"pg_restore","-U","postgres","--exit-on-error","--no-owner","--no-privileges","-d",targetDatabase],dump);
            var schema=await RunBytes("docker",["exec",scylla,"cqlsh","-e","DESCRIBE KEYSPACE "+keyspace+";"]);
            await Run("docker",["exec",scylla,"cqlsh","-e","DROP KEYSPACE "+keyspace+";"]);await RunBytes("docker",["exec","-i",scylla,"cqlsh"],schema);
            foreach(var key in settings.Keys.Where(k=>k.StartsWith("ConnectionStrings:")&&k!="ConnectionStrings:TradeTickQuoteDbConnection").ToArray())settings[key]=targetPg;
            settings["Storage:PostgresConnection"]=targetPg;settings["IFM_TEST_POSTGRES_CONNECTION"]=targetPg;
            var proof=new{version=1,state="RestoredNotReplayed",qualified=false,apiStarted=false,runId=run,fixtureComponentOnly=true,
                stores=new object[]{new{kind="postgres",container=postgres,database=targetDatabase},new{kind="scylla",container=scylla,keyspace}},
                endpoints=new{postgres=new{host="127.0.0.1",port=pgPort,database=targetDatabase,container=postgres},scylla=new{host="127.0.0.1",port=cqlPort,container=scylla},nats=new{host="127.0.0.1",port=natsPort,container=nats,url=broker}},
                ownerFiles=Directory.GetFiles(Path.Combine(root,"owners"),"*",SearchOption.AllDirectories).Where(p=>!p.EndsWith(".owner",StringComparison.Ordinal)).Select(p=>new{path=p,sha256=Hash(p)}).ToArray()};
            var certificate=Path.Combine(root,"restore.json");await File.WriteAllTextAsync(certificate,JsonSerializer.Serialize(proof));
            var token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));Env("IFM_REPLAY_CONTROL_TOKEN",token);Env("SessionCapture__AccessToken",token);
            var httpPort=FreePort();var url="http://127.0.0.1:"+httpPort;var config=Path.Combine(root,"host.json");
            await File.WriteAllTextAsync(config,JsonSerializer.Serialize(new{version=1,runId=run,restoreCertificatePath=certificate,restoreCertificateSha256=Hash(certificate),initialUtc=DateTimeOffset.UtcNow,natsUrl=broker,httpUrl=url,scope="replay-"+run,configuration=settings}));
            _=new HistoricalSessionReplay(config,"Test");
            var start=new ProcessStartInfo("dotnet"){WorkingDirectory=root,RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,CreateNoWindow=true};
            start.ArgumentList.Add(apiDll);start.ArgumentList.Add("--historical-session-replay="+config);start.Environment["DOTNET_ENVIRONMENT"]="Test";start.Environment["ASPNETCORE_ENVIRONMENT"]="Test";
            host=Process.Start(start)!;stdout=host.StandardOutput.ReadToEndAsync();stderr=host.StandardError.ReadToEndAsync();
            using var client=new HttpClient{BaseAddress=new Uri(url)};client.DefaultRequestHeaders.Add("X-IFM-Replay-Token",token);client.DefaultRequestHeaders.Add("X-IFM-Session-Capture",token);
            await Wait(async()=>{if(host.HasExited)throw new InvalidOperationException("Owned API exited before actor startup.");var status=await client.GetFromJsonAsync<JsonElement>("/qualification/replay/status");if(!status.GetProperty("actorReady").GetBoolean())throw new InvalidOperationException("Actual actors are not ready.");},TimeSpan.FromSeconds(180));
            Assert.Equal(System.Net.HttpStatusCode.OK,(await client.PostAsJsonAsync("/qualification/replay/clock",new{recordedArrivalUtc=DateTimeOffset.UtcNow,ordinal=1})).StatusCode);
            Env("IFM_NATS_URL",broker);Env("IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION",targetPg);Env("IFM_QUALIFICATION_ARTIFACT_DIRECTORY",root);
            Env("IFM_TEST_POSTGRES_CONNECTION",targetPg);Env("IFM_REPLAY_OWNER_ACCEPTANCE_RESTORE_CERTIFICATE",certificate);Env("IFM_REPLAY_OWNER_ACCEPTANCE_RESTORE_SHA256",Hash(certificate));
            await new PortfolioLiveHostEndToEndTests { BusinessUtcNow = () => DateTime.UtcNow.AddSeconds(-1) }.Production_NATS_actors_execute_configuration_resolution_reservation_composition_and_risk();
            var ownerBoundary=Guid.NewGuid().ToString("N");JsonElement ownerProof=default;
            await Wait(async()=>
            {
                var response=await client.PostAsync("/internal/session-capture/emulator/checkpoint/"+ownerBoundary,null);
                if(!response.IsSuccessStatusCode)throw new InvalidOperationException("Owner checkpoint was not certified: "+await response.Content.ReadAsStringAsync());
                ownerProof=await response.Content.ReadFromJsonAsync<JsonElement>();
            },TimeSpan.FromSeconds(30));
            Assert.Equal(restoredLedger,ownerProof.GetProperty("owner").GetProperty("effectivePath").GetString());
            Assert.True(ownerProof.GetProperty("financialValuesMatched").GetBoolean());
            Assert.True(ownerProof.GetProperty("owner").GetProperty("receipt").GetProperty("commitRevision").GetInt64()>0);
            var originalRequest=new TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine.EmulatorOwnerCheckpointRequest(Guid.ParseExact(ownerBoundary,"N"),ownerProof.GetProperty("owner").GetProperty("receipt").GetProperty("snapshot").GetProperty("asOfUtc").GetDateTime());
            using var originalRequestBody=new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(originalRequest));
            var duplicateProof=await client.PostAsync("/internal/session-capture/emulator/checkpoint/"+ownerBoundary,originalRequestBody);Assert.True(duplicateProof.IsSuccessStatusCode);
            var duplicate=await duplicateProof.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(ownerProof.GetProperty("owner").GetProperty("sha256").GetString(),duplicate.GetProperty("owner").GetProperty("sha256").GetString());
            Assert.Equal(ownerProof.GetProperty("persistedAccount").GetProperty("revision").GetInt32(),duplicate.GetProperty("persistedAccount").GetProperty("revision").GetInt32());
            await File.WriteAllTextAsync(Path.Combine(root,"owner-checkpoint-evidence.json"),ownerProof.GetRawText());
            await Wait(async()=>{var stop=await client.PostAsync("/qualification/replay/stop",null);if(stop.StatusCode!=System.Net.HttpStatusCode.Accepted)throw new InvalidOperationException("Actual actor/raw work has not drained.");},TimeSpan.FromSeconds(30));
            await host.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));Assert.Equal(0,host.ExitCode);
            await File.WriteAllTextAsync(Path.Combine(root,"component-result.json"),JsonSerializer.Serialize(new{qualified=false,component="ActualHistoricalApiPortfolioOwner",run,passed=true,restoredPostgresDumpSha256=Convert.ToHexString(SHA256.HashData(dump)),rawCqlRows=0,limitations=new[]{"Finite fixture, not complete-session evidence","No live source or broker orders","No native mapper/provider restoration claim"}}));
        }
        finally
        {
            if(host is{HasExited:false}){host.Kill(entireProcessTree:true);await host.WaitForExitAsync();}
            if(stdout is not null)await File.WriteAllTextAsync(Path.Combine(root,"api.stdout.log"),await stdout);if(stderr is not null)await File.WriteAllTextAsync(Path.Combine(root,"api.stderr.log"),await stderr);
            foreach(var pair in saved)Environment.SetEnvironmentVariable(pair.Key,pair.Value);
            Directory.SetCurrentDirectory(workingDirectory);
            foreach(var name in containers.AsEnumerable().Reverse())try{await Run("docker",["rm","-f","-v",name]);}catch{ /* Owned fixture remains for diagnosis only. */ }
            if(networkCreated)try{await Run("docker",["network","rm",network]);}catch{ }
        }
    }
    static string Hash(string path){using var file=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(file)).ToLowerInvariant();}
    static int FreePort(){var listener=new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,0);listener.Start();var port=((System.Net.IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();return port;}
    static async Task<int> Port(string name,string port)=>int.Parse((await Run("docker",["port",name,port+"/tcp"])).Trim().Split(':')[^1]);
    static async Task Wait(Func<Task> action,TimeSpan maximum){var until=DateTime.UtcNow+maximum;Exception? last=null;while(DateTime.UtcNow<until){try{await action();return;}catch(Exception error){last=error;await Task.Delay(500);}}throw new TimeoutException("Owned component fixture did not become ready.",last);}
    static async Task<string> Run(string file,IEnumerable<string> args)=>Encoding.UTF8.GetString(await RunBytes(file,args));
    static async Task<byte[]> RunBytes(string file,IEnumerable<string> args,byte[]? input=null)
    {
        var start=new ProcessStartInfo(file){RedirectStandardOutput=true,RedirectStandardError=true,RedirectStandardInput=input is not null,UseShellExecute=false,CreateNoWindow=true};foreach(var arg in args)start.ArgumentList.Add(arg);
        using var process=Process.Start(start)!;var output=new MemoryStream();var copy=process.StandardOutput.BaseStream.CopyToAsync(output);var errors=process.StandardError.ReadToEndAsync();
        if(input is not null){await process.StandardInput.BaseStream.WriteAsync(input);process.StandardInput.Close();}
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(5));await copy;await errors;if(process.ExitCode!=0||output.Length>64*1024*1024)throw new InvalidOperationException("Owned fixture provider command failed or exceeded its output bound.");return output.ToArray();
    }
}
