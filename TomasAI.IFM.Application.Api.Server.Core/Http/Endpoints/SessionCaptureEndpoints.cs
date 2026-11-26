using System.Net;
using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;

namespace TomasAI.IFM.Application.Api.Server.Core.Http.Endpoints;

/// <summary>Explicit capture diagnostics and opt-in non-economic owner checkpoints; no endpoint activates transport or submits orders.</summary>
public static class SessionCaptureEndpoints
{
    /// <summary>Maps an explicitly authenticated Development-only diagnostic.</summary>
    public static void MapSessionCaptureEndpoints(this WebApplication app)
    {
        app.MapSessionCaptureBoundaryEndpoints();
        bool Allowed(HttpContext request,bool ownerCheckpoint=false)
        {
            if (!(app.Environment.IsDevelopment()||ownerCheckpoint&&app.Environment.IsEnvironment("Test")&&TomasAI.IFM.Application.Api.Server.Core.Hosting.Modes.HistoricalSessionReplay.Active is not null) || request.Connection.RemoteIpAddress is not { } remote || !IPAddress.IsLoopback(remote)) return false;
            var configured = app.Configuration["SessionCapture:AccessToken"];
            var supplied = request.Request.Headers["X-IFM-Session-Capture"].ToString();
            return configured is { Length: >= 32 } && supplied.Length >= 32 && CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(configured)), SHA256.HashData(Encoding.UTF8.GetBytes(supplied)));
        }
        app.MapGet("/internal/session-capture/status", (HttpContext request, DurableRealtimeSessionArchive archive, DurableRealtimeMailboxHub hub) =>
        {
            if (!Allowed(request)) return Results.NotFound();
            return Results.Ok(new { version = 1, kind = "CaptureOnlySourceArchive", archive.Enabled,
                durableMailboxEnabled = hub.Enabled, archiveOwnershipOnly = true, qualifiedSession = false,
                archive.SessionDirectory, archive.PendingRecords, archive.RetainedBytes, archive.ReceiptBytes,
                failure = archive.CurrentFailure?.ToString(), matchingOwnerSnapshot = false });
        });
        app.MapGet("/internal/session-capture/prefix", async (HttpContext request, DurableRealtimeSessionArchive archive) =>
        {
            if (!Allowed(request) || !archive.Enabled) return Results.NotFound();
            return Results.Ok(await archive.GetCommittedPrefixAsync(request.RequestAborted).ConfigureAwait(false));
        });
        app.MapGet("/internal/session-capture/configuration",(HttpContext request)=>
        {
            if(!Allowed(request))return Results.NotFound();
            return Results.Ok(SessionCaptureConfiguration.Export(app.Configuration,app.Services.GetService<TimeProvider>()));
        });
        app.MapGet("/internal/session-capture/emulator/status",(HttpContext request)=>
        {
            if(!Allowed(request,ownerCheckpoint:true)||!app.Configuration.GetValue<bool>("SessionCapture:OwnerCheckpoints:Enabled"))return Results.NotFound();
            return Results.Ok(app.Services.GetRequiredService<SimpleInjector.Container>().GetInstance<TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine.EmulatorLedger>().OwnerCheckpointStatus());
        });
        app.MapPost("/internal/session-capture/emulator/checkpoint/{boundary}",async(HttpContext request,string boundary)=>
        {
            if(!Allowed(request,ownerCheckpoint:true)||!app.Configuration.GetValue<bool>("SessionCapture:OwnerCheckpoints:Enabled"))return Results.NotFound();
            if(!Guid.TryParseExact(boundary,"N",out var id)||id==Guid.Empty)return Results.BadRequest("A nonempty N-format boundary identity is required.");
            if(app.Environment.IsDevelopment())
            {
                var fence=app.Services.GetService<RealtimeSessionRootFence>()?.Snapshot();
                if(fence is not{Held:true,Active:0}||fence.Value.BoundaryId!=boundary)return Results.Conflict(new{kind="OwnerCheckpointRequiresHeldRootBoundary"});
                if(app.Configuration.GetValue<bool>("ResidentActorPilots:FuturesItiOrderedIntakeEnabled") &&
                    TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation.FuturesItiOrderedIngress.InspectDrain() is not{Drained:true})
                    return Results.Conflict(new{kind="OwnerCheckpointRequiresDrainedItiOwner"});
            }
            var container=app.Services.GetRequiredService<SimpleInjector.Container>();
            var supervisor=container.GetInstance<TomasAI.IFM.Shared.EventModelActor.Contracts.IActorSupervisor>();
            var work=supervisor.RuntimeContext.CaptureSnapshot();
            if(!supervisor.IsReady||work.QueuedMessageCount!=0||work.ProcessingMailboxCount!=0)return Results.Conflict(new{kind="OwnerCheckpointNotDrained",work.QueuedMessageCount,work.ProcessingMailboxCount});
            try
            {
                var ledger=container.GetInstance<TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine.EmulatorLedger>();
                TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine.EmulatorOwnerCheckpointRequest? original=null;
                if(request.Request.ContentLength!=0)
                {
                    var bytes=new byte[1025];var count=0;
                    while(count<bytes.Length){var read=await request.Request.Body.ReadAsync(bytes.AsMemory(count),request.RequestAborted);if(read==0)break;count+=read;}
                    if(count>0)
                    {
                        if(!app.Environment.IsEnvironment("Test")||count>1024)return Results.BadRequest("Original checkpoint requests are limited to isolated replay.");
                        original=System.Text.Json.JsonSerializer.Deserialize<TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine.EmulatorOwnerCheckpointRequest>(bytes.AsSpan(0,count));
                        if(original is null||original.BoundaryId!=id||original.RequestedAtUtc.Kind!=DateTimeKind.Utc)return Results.BadRequest("Original checkpoint identity is invalid.");
                    }
                }
                async ValueTask<DateTime> PrepareOriginalRequest()
                {
                    var frozen=ledger.FreezeOwnerCheckpointRequest(id,original?.RequestedAtUtc);
                if(app.Environment.IsDevelopment())
                {
                    var archive=app.Services.GetRequiredService<DurableRealtimeSessionArchive>();
                    if(!archive.Enabled)throw new InvalidOperationException("Owner checkpoint requires the source archive.");
                    var wire=System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(frozen);
                    await archive.RecordAsync(new(){Kind="EmulatorOwnerCheckpointRequest",LogicalSourceKey=id.ToString("N"),Subject="SessionCapture.EmulatorOwner.Checkpoint."+id.ToString("N"),EventId=id,OriginalWire=wire},request.RequestAborted).ConfigureAwait(false);
                }
                    return frozen.RequestedAtUtc;
                }
                var result=await EmulatorOwnerCheckpointCapture.CaptureAsync(ledger,
                    container.GetInstance<TomasAI.IFM.Domain.BrokerAccount.Realtime.BrokerAccountObservationBridge>(),container.GetInstance<TomasAI.IFM.Domain.BrokerAccount.Query.Model.IBrokerAccountReadStore>(),id,request.RequestAborted,original?.RequestedAtUtc,PrepareOriginalRequest).ConfigureAwait(false);
                return Results.Ok(result);
            }
            catch(Exception error) when(error is InvalidOperationException or IOException or TimeoutException)
            {return Results.Conflict(new{kind="OwnerCheckpointUncertified",reason=error.Message});}
        });    }
}
