using System.Net;
using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Application.Api.Server.Core.Http.Endpoints;

/// <summary>Opt-in Development boundary operations. A local drain is not a cross-owner snapshot.</summary>
public static class SessionCaptureBoundaryEndpoints
{
    public static void MapSessionCaptureBoundaryEndpoints(this WebApplication app)
    {
        if(app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("SessionCapture:Boundary:Enabled"))
            app.Use(async(context,next)=>
            {
                if(!context.Request.Path.StartsWithSegments("/internal/session-capture") && !context.Request.Path.StartsWithSegments("/health")
                    && context.RequestServices.GetRequiredService<RealtimeSessionRootFence>().Snapshot().Held)
                { context.Response.StatusCode=StatusCodes.Status503ServiceUnavailable;return; }
                await next(context).ConfigureAwait(false);
            });
        bool Allowed(HttpContext request) => app.Environment.IsDevelopment()
            && app.Configuration.GetValue<bool>("SessionCapture:Boundary:Enabled")
            && request.Connection.RemoteIpAddress is { } address && IPAddress.IsLoopback(address)
            && app.Configuration["SessionCapture:AccessToken"] is { Length: >=32 and <=256 } configured
            && request.Request.Headers["X-IFM-Session-Capture"].ToString() is { Length: >=32 and <=256 } supplied
            && CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(configured)),SHA256.HashData(Encoding.UTF8.GetBytes(supplied)));

        async Task<CaptureDrain> Inspect(HttpContext request)
        {
            var services=request.RequestServices;
            var archive=services.GetRequiredService<DurableRealtimeSessionArchive>();
            var fence=services.GetRequiredService<RealtimeSessionRootFence>().Snapshot();
            var supervisor=services.GetRequiredService<SimpleInjector.Container>().GetInstance<IActorSupervisor>();
            var hub=services.GetRequiredService<DurableRealtimeMailboxHub>();
            var broker=await services.GetRequiredService<NatsConnectionManager>().ProbeAsync(request.RequestAborted).ConfigureAwait(false);
            var raw=services.GetService<HostedServices.RawFuturesStorageConsumer>();
            var storage=raw is null?null:await raw.InspectDrainAsync(request.RequestAborted).ConfigureAwait(false);
            var actors=supervisor.RuntimeContext.CaptureSnapshot();
            var iti=TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation.FuturesItiOrderedIngress.InspectDrain();
            var itiExpected=app.Configuration.GetValue<bool>("ResidentActorPilots:FuturesItiOrderedIntakeEnabled");
            var drained=archive.Enabled && !hub.Enabled && fence.Held && fence.Active==0 && broker is not null
                && (!itiExpected || iti is {Drained:true})
                && supervisor.IsReady && actors.QueuedMessageCount==0 && actors.ProcessingMailboxCount==0
                && archive.PendingRecords==0 && archive.CurrentFailure is null
                && storage is not null && (!storage.Enabled || storage.Ready && storage.Pending==0 && storage.Unacknowledged==0 && storage.Failure is null);
            return new(fence.LeaseId,fence.BoundaryId,drained?"LocallyDrained":fence.Held?"Draining":"Running",
                fence.Held,fence.Active,actors.QueuedMessageCount,actors.ProcessingMailboxCount,
                storage?.Pending,storage?.Unacknowledged,archive.PendingRecords,archive.CurrentFailure,drained) {Iti=iti};
        }
        app.MapPost("/internal/session-capture/quiesce/{lease}/{boundary}",async Task<IResult>(HttpContext request,string lease,string boundary)=>
        {
            if(!Allowed(request))return Results.NotFound();
            if(!request.RequestServices.GetRequiredService<DurableRealtimeSessionArchive>().Enabled)return Results.Conflict(new{error="SOURCE_CAPTURE_DISABLED"});
            var fence=request.RequestServices.GetRequiredService<RealtimeSessionRootFence>();
            try { fence.Hold(lease,boundary); }
            catch(ArgumentException) { return Results.BadRequest(); }
            catch(InvalidOperationException) { return Results.Conflict(new{error="OTHER_CAPTURE_LEASE"}); }
            return Results.Ok(await Inspect(request).ConfigureAwait(false));
        });
        app.MapGet("/internal/session-capture/boundary",async Task<IResult>(HttpContext request)=>
            !Allowed(request)?Results.NotFound():Results.Ok(await Inspect(request).ConfigureAwait(false)));
        app.MapPost("/internal/session-capture/release/{lease}",(HttpContext request,string lease)=>
        {
            if(!Allowed(request))return Results.NotFound();
            try { request.RequestServices.GetRequiredService<RealtimeSessionRootFence>().Release(lease); return Results.Ok(new{state="Running",qualified=false}); }
            catch(InvalidOperationException) { return Results.Conflict(new{error="OTHER_CAPTURE_LEASE"}); }
        });
        app.MapPost("/internal/session-capture/stop/{lease}",async Task<IResult>(HttpContext request,string lease)=>
        {
            if(!Allowed(request))return Results.NotFound();
            var drain=await Inspect(request).ConfigureAwait(false);
            if(drain.LeaseId!=lease || !drain.LocallyDrained)return Results.Conflict(drain);
            // Stop is requested only after the HTTP receipt is flushed. The controller must independently
            // prove process exit and exclusive owner locks before declaring a cross-owner fence.
            request.Response.OnCompleted(()=>{request.RequestServices.GetRequiredService<IHostApplicationLifetime>().StopApplication();return Task.CompletedTask;});
            return Results.Accepted(value:new{state="GracefulShutdownRequested",leaseId=lease,qualified=false});
        });
    }
    public sealed record CaptureDrain(string? LeaseId,string? BoundaryId,string State,bool RootProducersPaused,int ActiveRootPublications,
        long ActorQueued,int ActorProcessing,ulong? RawPending,ulong? RawUnacknowledged,int ArchivePending,string? ArchiveFailure,bool LocallyDrained)
    { public int Version=>1;public bool Qualified=>false;public bool CrossOwnerSnapshot=>false;
        public TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation.FuturesItiOrderedDrainStatus? Iti {get;init;} }
}
