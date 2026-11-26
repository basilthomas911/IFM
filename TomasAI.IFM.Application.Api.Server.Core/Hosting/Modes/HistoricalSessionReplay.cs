using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Npgsql;

namespace TomasAI.IFM.Application.Api.Server.Core.Hosting.Modes;

/// <summary>Explicit Test-only composition of the real API against certified isolated restored stores.</summary>
public sealed class HistoricalSessionReplay
{
    public static HistoricalSessionReplay? Active { get; private set; }
    public HistoricalReplayClock Clock { get; }
    public string RunId { get; }
    public string RestoreCertificateSha256 { get; }
    public string BrokerUrl { get; }
    public string HttpUrl { get; }
    public string Scope { get; }
    public string LogPath { get; }
    public string? NativeCheckpointCertificatePath { get; }
    public string RestoreRoot { get; }
    readonly byte[] controlToken;
    readonly object controlGate=new();
    long lastOrdinal;
    DateTimeOffset? lastArrival;
    public IReadOnlyDictionary<string,string?> Settings { get; }

    public HistoricalSessionReplay(string configurationPath, string environment)
    {
        if (environment != "Test") throw new InvalidOperationException("Historical session replay requires the Test environment.");
        using var config = ReadBounded(configurationPath);
        var value = config.RootElement;
        if (value.GetProperty("version").GetInt32() != 1) throw new InvalidDataException("Unknown replay host configuration version.");
        RunId = value.GetProperty("runId").GetString()!;
        if (!Regex.IsMatch(RunId ?? "", "^[a-f0-9]{12}$")) throw new InvalidDataException("Invalid replay run identity.");
        var certificatePath = Path.GetFullPath(value.GetProperty("restoreCertificatePath").GetString()!);
        RestoreCertificateSha256 = HashFile(certificatePath);
        if (RestoreCertificateSha256 != value.GetProperty("restoreCertificateSha256").GetString()) throw new InvalidDataException("Restore certificate changed.");
        using var certificate = ReadBounded(certificatePath);
        var proof = certificate.RootElement;
        if (proof.GetProperty("version").GetInt32()!=1 || proof.GetProperty("state").GetString()!="RestoredNotReplayed" ||
            proof.GetProperty("runId").GetString()!=RunId || proof.GetProperty("qualified").GetBoolean() || proof.GetProperty("apiStarted").GetBoolean())
            throw new InvalidDataException("Replay requires an unstarted isolated restore certificate.");
        var stores=proof.GetProperty("stores").EnumerateArray().ToArray();
        var endpoints=proof.GetProperty("endpoints");
        var pgEndpoint=endpoints.GetProperty("postgres");var cqlEndpoint=endpoints.GetProperty("scylla");
        var brokerEndpoint=endpoints.GetProperty("nats");
        static int CertifiedPort(JsonElement endpoint)
        {
            var port=endpoint.GetProperty("port").GetInt32();
            if(endpoint.GetProperty("host").GetString()!="127.0.0.1" || port is <1 or >65535 || new[]{4222,5432,9042,22543,22544}.Contains(port))throw new InvalidDataException("Replay requires certified isolated published endpoints.");
            return port;
        }
        var pgPort=CertifiedPort(pgEndpoint);var cqlPort=CertifiedPort(cqlEndpoint);_=CertifiedPort(brokerEndpoint);
        var pg=stores.Single(x=>x.GetProperty("kind").GetString()=="postgres");
        var databases=new HashSet<string>([pg.GetProperty("database").GetString()!],StringComparer.Ordinal);
        if(!databases.Contains(pgEndpoint.GetProperty("database").GetString()!))throw new InvalidDataException("Certified PG endpoint and restored database differ.");
        var keyspaces=stores.Where(x=>x.GetProperty("kind").GetString()=="scylla").Select(x=>x.GetProperty("keyspace").GetString()!).ToHashSet(StringComparer.Ordinal);
        if(keyspaces.Count==0)throw new InvalidDataException("Restored Scylla keyspaces are required.");
        var root=Path.GetDirectoryName(certificatePath)!;
        RestoreRoot=root;
        LogPath=Path.Combine(root,"replay-host-logs","ifm-apiserver-.log");
        var restoredPaths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var file in proof.GetProperty("ownerFiles").EnumerateArray())
        {
            var path=Path.GetFullPath(file.GetProperty("path").GetString()!);
            RequireChild(root,path);
            if(!restoredPaths.Add(path))throw new InvalidDataException("Duplicate restored owner file identity.");
            if(HashFile(path)!=file.GetProperty("sha256").GetString())
                throw new InvalidDataException("Restored owner file changed before startup.");
        }
        BrokerUrl=LocalUri(value.GetProperty("natsUrl").GetString()!,"nats");
        if(BrokerUrl!=brokerEndpoint.GetProperty("url").GetString())throw new InvalidDataException("Replay broker differs from certified owned endpoint.");
        HttpUrl=LocalUri(value.GetProperty("httpUrl").GetString()!,"http");
        Scope=value.GetProperty("scope").GetString()!;
        if(value.TryGetProperty("nativeCheckpointCertificatePath",out var nativePath))
        {
            NativeCheckpointCertificatePath=Path.GetFullPath(nativePath.GetString()!);RequireChild(root,NativeCheckpointCertificatePath);
            if(!restoredPaths.Contains(NativeCheckpointCertificatePath)||HashFile(NativeCheckpointCertificatePath)!=value.GetProperty("nativeCheckpointCertificateSha256").GetString())throw new InvalidDataException("Native checkpoint certificate is not an original hash-certified restored owner file.");
            using var native=ReadBounded(NativeCheckpointCertificatePath);var original=native.RootElement;
            if(original.GetProperty("Version").GetInt32()!=1||original.GetProperty("Kind").GetString()!="FrozenNativeAndBackendSnapshot"||original.GetProperty("Qualified").GetBoolean()||original.GetProperty("CrossOwnerSnapshot").GetBoolean()||original.GetProperty("Scope").GetString()!=Scope||!Regex.IsMatch(Scope??"","^[A-Za-z0-9-]{1,128}$"))throw new InvalidDataException("Original native checkpoint namespace differs from replay configuration.");
        }
        else if(!Regex.IsMatch(Scope??"","^replay-[a-f0-9]{12}$") || Scope!="replay-"+RunId)throw new InvalidDataException("A component fixture without a native checkpoint must use its run-specific scope.");
        Clock=new HistoricalReplayClock(value.GetProperty("initialUtc").GetDateTimeOffset());
        if(RecordedMonitoringAuthority.LoadFromConfiguration(value,RestoreRoot) is{} authority)Clock.SetGenerationAuthority(authority);
        var token=Environment.GetEnvironmentVariable("IFM_REPLAY_CONTROL_TOKEN");
        if(token is null || token.Length is <32 or >256)throw new InvalidDataException("Replay control requires a bounded protected environment token.");
        controlToken=System.Text.Encoding.UTF8.GetBytes(token);
        var settings=value.GetProperty("configuration").EnumerateObject().ToDictionary(x=>x.Name,x=>x.Value.ValueKind==JsonValueKind.Null?null:x.Value.GetString(),StringComparer.OrdinalIgnoreCase);
        foreach(var pair in settings.Where(x=>x.Key.StartsWith("ConnectionStrings:",StringComparison.OrdinalIgnoreCase)))
        {
            if(string.IsNullOrWhiteSpace(pair.Value))throw new InvalidDataException("Missing restored connection mapping.");
            if(pair.Value.Contains("Contact Points=",StringComparison.OrdinalIgnoreCase))
            {
                var parts=pair.Value.Split(';',StringSplitOptions.RemoveEmptyEntries).Select(x=>x.Split('=',2)).ToDictionary(x=>x[0].Trim(),x=>x[1].Trim(),StringComparer.OrdinalIgnoreCase);
                if(parts.GetValueOrDefault("Contact Points")!="127.0.0.1" || !keyspaces.Contains(parts.GetValueOrDefault("Default Keyspace")??"") || !int.TryParse(parts.GetValueOrDefault("Port"),out var port) || port!=cqlPort)
                    throw new InvalidDataException("CQL connection is outside the restored isolated stores.");
            }
            else
            {
                var pgConnection=new NpgsqlConnectionStringBuilder(pair.Value);
                if(!string.IsNullOrWhiteSpace(pgConnection.Username)||!string.IsNullOrWhiteSpace(pgConnection.Password))throw new InvalidDataException("Replay base connections must use protected Test credential environment variables.");
                if(pgConnection.Host!="127.0.0.1" || pgConnection.Port!=pgPort || !databases.Contains(pgConnection.Database!))throw new InvalidDataException("PG connection is outside restored isolated stores.");
            }
        }
        foreach(var key in OwnerPathKeys)
        {
            if(!settings.TryGetValue(key,out var path) || string.IsNullOrWhiteSpace(path))throw new InvalidDataException("Missing explicit restored owner mapping: "+key);
            RequireChild(root,Path.GetFullPath(path));
        }
        if(settings["TradeBroker:Emulator:LedgerPath"] is not{} ledger || !File.Exists(ledger) || !restoredPaths.Contains(Path.GetFullPath(ledger)))throw new InvalidDataException("An authentic hash-certified restored emulator ledger is required.");
        if(!settings.TryGetValue("ConnectionStrings:EventSourceActorDbConnection",out var applicationPostgres)||string.IsNullOrWhiteSpace(applicationPostgres)||!settings.ContainsKey("ConnectionStrings:TradeTickQuoteDbConnection"))throw new InvalidDataException("Explicit canonical PostgreSQL and raw Scylla mappings are required.");
        settings["IFM_TEST_POSTGRES_CONNECTION"]=applicationPostgres;settings["Storage:PostgresConnection"]=applicationPostgres;
        foreach(var (key,leaf) in new[]{("ScheduledTasks:OutputRoot","scheduled-output"),("DatabaseBackup:OutputRoot","backup-output")})
        {
            if(settings.TryGetValue(key,out var path)&&!string.IsNullOrWhiteSpace(path))RequireChild(root,Path.GetFullPath(path));
            else settings[key]=Path.Combine(root,"owners",leaf);
        }
        settings["urls"]=HttpUrl;settings["Kestrel:Endpoints:Http:Url"]=HttpUrl;
        settings["IFM_TEST_NATS_URL"]=BrokerUrl;settings["Nats:Consumer:Url"]=BrokerUrl;settings["Nats:JetStreamConsumer:Url"]=BrokerUrl;
        settings["MarketDataHost:Enabled"]="true";settings["MarketDataHost:NatsUrl"]=BrokerUrl;settings["MarketDataHost:Scope"]=Scope;
        settings["AppSettings:Fmp:Enabled"]="false";settings["AppSettings:HistoricalAnalyticsWarmup:Enabled"]="false";
        settings["AppSettings:IntrinsicTimeStrategyWorkflow:DevelopmentPortfolio:Enabled"]="false";
        settings["AppSettings:IntrinsicTimeStrategyWorkflow:ProvisionDevelopmentMarketConditionAssessmentDefaults"]="false";
        settings["TradeBroker:Emulator:OfflineSimulation:Enabled"]="false";
        settings["Actors:SessionCapture:Enabled"]="false";
        settings["IFM_TEST_ACTOR_DOMAIN"]=null;
        settings["Telemetry:Metrics:Enabled"]="false";settings["Telemetry:Logs:Enabled"]="false";settings["Telemetry:Tracing:Enabled"]="false";
        settings["AzureStorage:ConnectionString"]=null;
        settings["Telemetry:GcHistory:OutputDirectory"]=Path.Combine(root,"replay-host-logs","gc");
        Settings=settings;
    }

    public static readonly string[] OwnerPathKeys = ["Actors:RealtimeMailboxes:Directory","RawFuturesStorage:Directory","TradeBroker:Emulator:LedgerPath","ResidentActorPilots:FuturesItiJournalDirectory",
        ..new[]{"FuturesOptionRouting","FuturesRouting","DailyFuturesExit","WorkflowInitialization","WorkflowComposition","WorkflowMarketCondition","WorkflowReservation","WorkflowCompositionFunction","WorkflowSelectionFunction","WorkflowAssessmentFunction","WorkflowRegimeFunction","WorkflowPublication","TradeSessionBar","ItiIngress","TdiResult","IronCondorPosition"}.Select(x=>"AppSettings:"+x+"Recovery:Directory")];

    public void MapControlEndpoints(WebApplication app)
    {
        app.MapGet("/qualification/replay/status",async Task<IResult> (HttpContext http)=>
        {
            if(!Authorized(http))return Results.Unauthorized();
            var supervisor=http.RequestServices.GetService<TomasAI.IFM.Shared.EventModelActor.Contracts.IActorSupervisor>();
            var hub=http.RequestServices.GetService<TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes.DurableRealtimeMailboxHub>();
            var actorWork=supervisor?.RuntimeContext.CaptureSnapshot();
            var raw=http.RequestServices.GetService<HostedServices.RawFuturesStorageConsumer>();
            var rawDrain=raw is null?null:await raw.InspectDrainAsync(http.RequestAborted);
            var iti=TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation.FuturesItiOrderedIngress.InspectDrain();
            return Results.Json(new {RunId,RestoreCertificateSha256,Scope,nativeCheckpointBound=NativeCheckpointCertificatePath is not null,utc=Clock.GetUtcNow(),clockRunning=Clock.IsRunning,qualified=false,productionHandlers=true,
                physicalDeadlines="SystemMonotonic",actorReady=supervisor?.IsReady??false,hubRunning=hub?.IsRunning??false,
                pendingSources=hub?.PendingSourceCount,mailboxes=hub?.MailboxCount,brokerPending=hub?.BrokerPendingMessages,
                brokerUnacknowledged=hub?.BrokerUnacknowledgedMessages,failure=hub?.CurrentFailure,
                actorQueued=actorWork?.QueuedMessageCount,actorProcessing=actorWork?.ProcessingMailboxCount,raw=rawDrain,
                itiRequired=app.Configuration.GetValue<bool>("ResidentActorPilots:FuturesItiOrderedIntakeEnabled"),iti});
        });
        app.MapPost("/qualification/replay/clock",async Task<IResult> (HttpContext http)=>
        {
            if(!Authorized(http))return Results.Unauthorized();
            if(http.Request.ContentLength is >1024)return Results.BadRequest(new{error="REPLAY_CLOCK_BODY_BOUNDS"});
            try
            {
                using var readDeadline=CancellationTokenSource.CreateLinkedTokenSource(http.RequestAborted);readDeadline.CancelAfter(TimeSpan.FromSeconds(2));
                var bytes=new byte[1025];var count=0;
                while(count<bytes.Length)
                {
                    var read=await http.Request.Body.ReadAsync(bytes.AsMemory(count),readDeadline.Token);
                    if(read==0)break;count+=read;
                }
                if(count is 0 or >1024)return Results.BadRequest(new{error="REPLAY_CLOCK_BODY_BOUNDS"});
                var request=JsonSerializer.Deserialize<ReplayClockAdvance>(bytes.AsSpan(0,count),JsonSerializerOptions.Web);
                if(request is null)return Results.BadRequest();
                lock(controlGate)
                {
                    if(request.Ordinal==lastOrdinal && lastArrival==request.RecordedArrivalUtc)return Results.Json(new{utc=Clock.GetUtcNow(),ordinal=lastOrdinal,idempotent=true,qualified=false});
                    if(request.Ordinal!=lastOrdinal+1)throw new InvalidOperationException("Replay clock ordinals must be contiguous.");
                    Clock.AdvanceTo(request.RecordedArrivalUtc);lastOrdinal=request.Ordinal;lastArrival=request.RecordedArrivalUtc;
                    return Results.Json(new{utc=Clock.GetUtcNow(),ordinal=lastOrdinal,idempotent=false,qualified=false});
                }
            }
            catch(InvalidOperationException){return Results.Conflict(new{error="REPLAY_CLOCK_BACKWARDS_OR_LAGGING"});}
            catch(JsonException){return Results.BadRequest(new{error="REPLAY_CLOCK_BODY_INVALID"});}
            catch(OperationCanceledException){return Results.StatusCode(StatusCodes.Status408RequestTimeout);}
        });
        app.MapPost("/qualification/replay/stop",async Task<IResult> (HttpContext http)=>
        {
            if(!Authorized(http))return Results.Unauthorized();
            var iti=TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation.FuturesItiOrderedIngress.InspectDrain();
            if(app.Configuration.GetValue<bool>("ResidentActorPilots:FuturesItiOrderedIntakeEnabled") && iti is not{Drained:true})
                return Results.Conflict(new{error="REPLAY_ITI_WORK_PENDING_OR_FAILED",iti,qualified=false});
            var hub=http.RequestServices.GetService<TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes.DurableRealtimeMailboxHub>();
            var actorWork=http.RequestServices.GetService<TomasAI.IFM.Shared.EventModelActor.Contracts.IActorSupervisor>()?.RuntimeContext.CaptureSnapshot();
            var raw=http.RequestServices.GetService<HostedServices.RawFuturesStorageConsumer>();
            var rawDrain=raw is null?null:await raw.InspectDrainAsync(http.RequestAborted);
            if(actorWork is{QueuedMessageCount:>0} or {ProcessingMailboxCount:>0} || rawDrain is{Enabled:true} && (!rawDrain.Ready||rawDrain.Pending!=0||rawDrain.Unacknowledged!=0))
                return Results.Conflict(new{error="REPLAY_ACTOR_OR_RAW_WORK_PENDING",qualified=false});
            if(hub is{Enabled:true} && (hub.PendingSourceCount!=0||hub.BrokerPendingMessages!=0||hub.BrokerUnacknowledgedMessages!=0))
                return Results.Conflict(new{error="REPLAY_REQUIRED_WORK_PENDING",qualified=false});
            http.Response.OnCompleted(()=>{http.RequestServices.GetRequiredService<IHostApplicationLifetime>().StopApplication();return Task.CompletedTask;});
            return Results.Accepted(value:new{state="GracefulShutdownRequested",qualified=false});
        });
    }
    bool Authorized(HttpContext context)
    {
        if(context.Connection.RemoteIpAddress is not{} ip || !System.Net.IPAddress.IsLoopback(ip))return false;
        var supplied=context.Request.Headers["X-IFM-Replay-Token"].ToString();
        if(supplied.Length>256)return false;
        return CryptographicOperations.FixedTimeEquals(controlToken,System.Text.Encoding.UTF8.GetBytes(supplied));
    }
    public sealed record ReplayClockAdvance(DateTimeOffset RecordedArrivalUtc,long Ordinal);

    public static void Configure(WebApplicationBuilder builder,string[] args)
    {
        var flags=args.Where(x=>x.StartsWith("--historical-session-replay=",StringComparison.Ordinal)).ToArray();
        if(flags.Length==0)return;
        if(flags.Length!=1 || args.Any(x=>x.StartsWith("--event-log-qualification",StringComparison.Ordinal) || x.EndsWith("-only",StringComparison.Ordinal)))throw new InvalidOperationException("Replay cannot combine other maintenance modes.");
        foreach(var key in new[]{"DATABENTO_API_KEY","FMP_API_KEY"})if(!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))throw new InvalidOperationException("Replay forbids live provider credentials.");
        var mode=new HistoricalSessionReplay(flags[0]["--historical-session-replay=".Length..],builder.Environment.EnvironmentName);
        foreach(var child in builder.Configuration.GetSection("ConnectionStrings").GetChildren())if(!mode.Settings.ContainsKey("ConnectionStrings:"+child.Key))throw new InvalidDataException("Unmapped inherited database connection: "+child.Key);
        foreach(var endpoint in builder.Configuration.GetSection("Kestrel:Endpoints").GetChildren())if(endpoint.Key!="Http")throw new InvalidDataException("Replay forbids additional inherited listener endpoints.");
        mode.ApplyTo(builder);
        Environment.SetEnvironmentVariable("IFM_API_RECOVERY_CONTROLLER",null);
        Environment.SetEnvironmentVariable("IFM_API_RECOVERY_DIRECTORY",null);
        Environment.SetEnvironmentVariable("IFM_DEVELOPMENT_SESSION_ID",null);
        Active=mode;
    }
    internal void ApplyTo(WebApplicationBuilder builder)
    {
        if(!builder.Environment.IsEnvironment("Test"))throw new InvalidOperationException("Historical replay cannot change a non-Test host composition.");
        builder.Configuration.AddInMemoryCollection(Settings);
        // Supervisor/indicator persistence derives paths from IHostEnvironment, not configuration.
        builder.Environment.ContentRootPath=RestoreRoot;
        builder.Environment.ContentRootFileProvider=new Microsoft.Extensions.FileProviders.PhysicalFileProvider(RestoreRoot);
        builder.Services.AddSingleton<IHostEnvironment>(builder.Environment);
        builder.Services.AddSingleton(this);builder.Services.AddSingleton(Clock);builder.Services.AddSingleton<TimeProvider>(Clock);
        builder.Services.AddSingleton<Microsoft.Extensions.Http.IHttpMessageHandlerBuilderFilter,ReplayHttpFilter>();
    }
    static JsonDocument ReadBounded(string path)
    {
        if(new FileInfo(path).Length>4*1024*1024)throw new InvalidDataException("Replay configuration/certificate exceeds metadata bound.");
        return JsonDocument.Parse(File.ReadAllBytes(path));
    }
    static string HashFile(string path)
    {
        using var stream=File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
    static void RequireChild(string root,string path)
    {
        var relative=Path.GetRelativePath(root,path);
        if(Path.IsPathRooted(relative)||relative==".."||relative.StartsWith(".."+Path.DirectorySeparatorChar,StringComparison.Ordinal))throw new InvalidDataException("Replay owner path escapes the restored root.");
        for(var current=path;current is not null;current=Path.GetDirectoryName(current))
        {
            if((File.Exists(current)||Directory.Exists(current)) && (File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Replay owner paths cannot traverse filesystem links.");
            if(string.Equals(current,root,StringComparison.OrdinalIgnoreCase))break;
        }
    }
    static string LocalUri(string value,string scheme)
    {
        if(!Uri.TryCreate(value,UriKind.Absolute,out var uri)||uri.Scheme!=scheme||uri.Host!="127.0.0.1"||uri.UserInfo.Length!=0||uri.Query.Length!=0||uri.Fragment.Length!=0||uri.Port<=0)throw new InvalidDataException("Replay endpoints must be explicit loopback endpoints.");
        return value;
    }
    sealed class ReplayHttpFilter : Microsoft.Extensions.Http.IHttpMessageHandlerBuilderFilter
    {
        public Action<Microsoft.Extensions.Http.HttpMessageHandlerBuilder> Configure(Action<Microsoft.Extensions.Http.HttpMessageHandlerBuilder> next)=>builder=>{next(builder);builder.AdditionalHandlers.Insert(0,new DenyExternal());};
        sealed class DenyExternal : DelegatingHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
                =>throw new InvalidOperationException("Historical replay forbids outbound HTTP provider access; required inputs must be restored.");
        }
    }
}
