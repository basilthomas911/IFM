using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using System.Net;
using System.Net.Http.Json;
using TomasAI.IFM.Application.Api.Server.Core.Hosting.Modes;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

public sealed class HistoricalSessionReplayTests
{
    [Fact]
    public async Task Bootstrap_freezes_only_business_UTC_until_first_authenticated_arrival_while_physical_time_runs()
    {
        var initial=DateTimeOffset.Parse("2025-11-03T14:30:00Z");var clock=new HistoricalReplayClock(initial);var stamp=clock.GetTimestamp();
        await Task.Delay(30);Assert.Equal(initial,clock.GetUtcNow());Assert.False(clock.IsRunning);Assert.True(clock.GetElapsedTime(stamp)>TimeSpan.Zero);
        clock.AdvanceTo(initial);await Task.Delay(20);Assert.True(clock.IsRunning);Assert.True(clock.GetUtcNow()>initial);
        var before=clock.GetUtcNow();clock.AdvanceTo(initial);Assert.True(clock.GetUtcNow()>=before);
    }
    [Fact]
    public async Task Historical_UTC_does_not_change_elapsed_time_or_timer_deadlines()
    {
        var clock=new HistoricalReplayClock(DateTimeOffset.Parse("2025-11-03T14:30:00Z"));
        var timestamp=clock.GetTimestamp();
        var fired=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var timer=clock.CreateTimer(_=>fired.TrySetResult(),null,TimeSpan.FromMilliseconds(30),Timeout.InfiniteTimeSpan);
        clock.AdvanceTo(DateTimeOffset.Parse("2026-11-03T14:30:00Z"));
        await fired.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.InRange(clock.GetElapsedTime(timestamp).TotalSeconds,0.01,5);
        Assert.Equal(2026,clock.GetUtcNow().Year);
        Assert.Throws<InvalidOperationException>(()=>clock.AdvanceTo(DateTimeOffset.Parse("2025-11-03T14:30:00Z")));
    }

    [Fact]
    public void Actual_mode_preserves_restored_store_owner_mappings_and_never_synthesizes_inputs()
    {
        using var fixture=new Fixture();
        var mode=new HistoricalSessionReplay(fixture.ConfigPath,"Test");
        Assert.Equal("replay-012345abcdef",mode.Scope);
        Assert.Equal(fixture.Settings["ConnectionStrings:TradeDbConnection"],mode.Settings["ConnectionStrings:TradeDbConnection"]);
        Assert.Equal(fixture.Settings["TradeBroker:Emulator:LedgerPath"],mode.Settings["TradeBroker:Emulator:LedgerPath"]);
        Assert.Equal("true",mode.Settings["MarketDataHost:Enabled"]);
        Assert.Equal("false",mode.Settings["TradeBroker:Emulator:OfflineSimulation:Enabled"]);
        Assert.Equal("true",mode.Settings["AppSettings:IntrinsicTimeStrategyWorkflow:Enabled"]);
        Assert.DoesNotContain(mode.Settings.Keys,key=>key.Contains("Synthetic",StringComparison.Ordinal));
        Assert.DoesNotContain(mode.Settings.Keys,key=>key.StartsWith("EventLogPersistence",StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("live-store")]
    [InlineData("owner-escape")]
    [InlineData("changed-owner")]
    [InlineData("changed-certificate")]
    [InlineData("not-restored")]
    [InlineData("wrong-pg-port")]
    [InlineData("wrong-cql-port")]
    public void Invalid_restored_boundary_is_rejected_before_host_composition(string mutation)
    {
        using var fixture=new Fixture();
        switch(mutation)
        {
            case "live-store":fixture.Settings["ConnectionStrings:TradeDbConnection"]="Host=192.0.2.1;Database=real";fixture.Save();break;
            case "owner-escape":fixture.Settings["Actors:RealtimeMailboxes:Directory"]=Path.GetTempPath();fixture.Save();break;
            case "changed-owner":File.AppendAllText(fixture.Ledger,"changed");break;
            case "changed-certificate":File.AppendAllText(fixture.Certificate," ");break;
            case "not-restored":fixture.Proof["state"]="Incomplete";fixture.Save();break;
            case "wrong-pg-port":fixture.Settings["ConnectionStrings:TradeDbConnection"]="Host=127.0.0.1;Port=5432;Database=restored_actual";fixture.Save();break;
            case "wrong-cql-port":fixture.Settings["ConnectionStrings:TradeTickQuoteDbConnection"]="Contact Points=127.0.0.1;Port=9042;Default Keyspace=restored_actual";fixture.Save();break;
        }
        Assert.ThrowsAny<Exception>(()=>new HistoricalSessionReplay(fixture.ConfigPath,"Test"));
    }

    [Fact]
    public void Replay_mode_is_unavailable_in_Development()
    {
        using var fixture=new Fixture();
        Assert.Throws<InvalidOperationException>(()=>new HistoricalSessionReplay(fixture.ConfigPath,"Development"));
    }
    [Fact]
    public async Task Actual_host_environment_and_resident_journal_paths_are_inside_the_restored_boundary()
    {
        using var fixture=new Fixture();var mode=new HistoricalSessionReplay(fixture.ConfigPath,"Test");
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions{EnvironmentName="Test"});mode.ApplyTo(builder);
        await using var app=builder.Build();
        Assert.Equal(mode.RestoreRoot,app.Environment.ContentRootPath);
        Assert.Equal(mode.RestoreRoot,app.Services.GetRequiredService<Microsoft.Extensions.Hosting.IHostEnvironment>().ContentRootPath);
        Assert.Same(mode.Clock,app.Services.GetRequiredService<TimeProvider>());
        Assert.StartsWith(mode.RestoreRoot,builder.Configuration["ResidentActorPilots:FuturesItiJournalDirectory"]);
        Assert.StartsWith(mode.RestoreRoot,builder.Configuration["Telemetry:GcHistory:OutputDirectory"]);
        Assert.Equal(mode.Settings["ConnectionStrings:EventSourceActorDbConnection"],builder.Configuration["IFM_TEST_POSTGRES_CONNECTION"]);
    }
    [Fact]
    public void Original_native_namespace_requires_the_hash_certified_restored_checkpoint_certificate()
    {
        using var fixture=new Fixture();fixture.Scope="development";fixture.Save();
        Assert.Throws<InvalidDataException>(()=>new HistoricalSessionReplay(fixture.ConfigPath,"Test"));
        fixture.NativePath=Path.Combine(Path.GetDirectoryName(fixture.Certificate)!,"owners","native-checkpoint.json");
        File.WriteAllText(fixture.NativePath,JsonSerializer.Serialize(new{Version=1,Kind="FrozenNativeAndBackendSnapshot",Scope="development",Qualified=false,CrossOwnerSnapshot=false}));
        fixture.Proof["ownerFiles"]=new[]{new{path=fixture.Ledger,sha256=Fixture.Hash(fixture.Ledger)},new{path=fixture.NativePath,sha256=Fixture.Hash(fixture.NativePath)}};fixture.Save();
        var mode=new HistoricalSessionReplay(fixture.ConfigPath,"Test");Assert.Equal("development",mode.Scope);Assert.Equal(fixture.NativePath,mode.NativeCheckpointCertificatePath);
        File.AppendAllText(fixture.NativePath," ");Assert.Throws<InvalidDataException>(()=>new HistoricalSessionReplay(fixture.ConfigPath,"Test"));
    }

    [Fact]
    public async Task Protected_clock_endpoint_keeps_exact_arrival_retry_and_rejects_skips_or_rewinds()
    {
        using var fixture=new Fixture();var mode=new HistoricalSessionReplay(fixture.ConfigPath,"Test");
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions{EnvironmentName="Test"});builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var app=builder.Build();mode.MapControlEndpoints(app);await app.StartAsync();
        var address=app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client=new HttpClient{BaseAddress=new Uri(address)};
        var arrival=DateTimeOffset.Parse("2025-11-03T14:30:00Z");
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsJsonAsync("/qualification/replay/clock",new{recordedArrivalUtc=arrival,ordinal=1})).StatusCode);
        client.DefaultRequestHeaders.Add("X-IFM-Replay-Token",new string('a',48));
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync("/qualification/replay/clock",new{recordedArrivalUtc=arrival,ordinal=1})).StatusCode);
        await Task.Delay(15); // The repeated recorded arrival is behind real elapsed UTC, but must not rewind or falsely fail.
        using(var repeat=await client.PostAsJsonAsync("/qualification/replay/clock",new{recordedArrivalUtc=arrival,ordinal=1}))
        {Assert.Equal(HttpStatusCode.OK,repeat.StatusCode);Assert.Contains("\"idempotent\":true",await repeat.Content.ReadAsStringAsync());}
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync("/qualification/replay/clock",new{recordedArrivalUtc=arrival,ordinal=3})).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync("/qualification/replay/clock",new{recordedArrivalUtc=arrival.AddSeconds(-1),ordinal=2})).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync("/qualification/replay/clock",new{recordedArrivalUtc=arrival,ordinal=2})).StatusCode);
        Assert.True(mode.Clock.GetUtcNow()>arrival);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsync("/qualification/replay/clock",new StringContent(new string('x',1025)))).StatusCode);
        await app.StopAsync();
    }

    sealed class Fixture : IDisposable
    {
        readonly string root=Path.Combine(Path.GetTempPath(),"ifm-replay-host-"+Guid.NewGuid().ToString("N"));
        readonly string? priorToken=Environment.GetEnvironmentVariable("IFM_REPLAY_CONTROL_TOKEN");
        public string ConfigPath=>Path.Combine(root,"host.json");
        public string Certificate=>Path.Combine(root,"restore.json");
        public string Ledger=>Path.Combine(root,"owners","emulator","ledger.json");
        public string Scope {get;set;}="replay-012345abcdef";
        public string? NativePath {get;set;}
        public Dictionary<string,string?> Settings {get;}=new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string,object?> Proof {get;}=new();
        public Fixture()
        {
            Environment.SetEnvironmentVariable("IFM_REPLAY_CONTROL_TOKEN",new string('a',48));
            Directory.CreateDirectory(Path.GetDirectoryName(Ledger)!);File.WriteAllText(Ledger,"authentic-fixture-not-a-trading-ledger");
            foreach(var key in HistoricalSessionReplay.OwnerPathKeys)
            {
                var path=key=="TradeBroker:Emulator:LedgerPath"?Ledger:Path.Combine(root,"owners",key.Replace(':','_'));
                if(path!=Ledger)Directory.CreateDirectory(path);Settings[key]=path;
            }
            Settings["ConnectionStrings:TradeDbConnection"]="Host=127.0.0.1;Port=45678;Database=restored_actual";
            Settings["ConnectionStrings:EventSourceActorDbConnection"]=Settings["ConnectionStrings:TradeDbConnection"];
            Settings["ConnectionStrings:TradeTickQuoteDbConnection"]="Contact Points=127.0.0.1;Port=49042;Default Keyspace=restored_actual";
            Settings["AppSettings:IntrinsicTimeStrategyWorkflow:Enabled"]="true";
            Proof["version"]=1;Proof["runId"]="012345abcdef";Proof["qualified"]=false;Proof["apiStarted"]=false;Proof["state"]="RestoredNotReplayed";
            Proof["stores"]=new object[]{new{kind="postgres",database="restored_actual"},new{kind="scylla",keyspace="restored_actual"}};
            Proof["endpoints"]=new{postgres=new{host="127.0.0.1",port=45678,database="restored_actual"},scylla=new{host="127.0.0.1",port=49042},nats=new{host="127.0.0.1",port=45222,url="nats://127.0.0.1:45222"}};
            Proof["ownerFiles"]=new[]{new{path=Ledger,sha256=Hash(Ledger)}};Save();
        }
        public void Save()
        {
            File.WriteAllText(Certificate,JsonSerializer.Serialize(Proof));
            var config=new Dictionary<string,object?>{{"version",1},{"runId","012345abcdef"},{"restoreCertificatePath",Certificate},{"restoreCertificateSha256",Hash(Certificate)},{"initialUtc","2025-11-03T14:30:00Z"},{"natsUrl","nats://127.0.0.1:45222"},{"httpUrl","http://127.0.0.1:25444"},{"scope",Scope},{"configuration",Settings}};
            if(NativePath is not null){config["nativeCheckpointCertificatePath"]=NativePath;config["nativeCheckpointCertificateSha256"]=Hash(NativePath);}
            File.WriteAllText(ConfigPath,JsonSerializer.Serialize(config));
        }
        public static string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
        public void Dispose(){Environment.SetEnvironmentVariable("IFM_REPLAY_CONTROL_TOKEN",priorToken);Directory.Delete(root,true);}
    }
}
