using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace TomasAI.IFM.Application.MarketData.Rust.Host;

/// <summary>Explicit local-development native forwarding control, never a cross-owner snapshot attestation.</summary>
public static class NativeSessionCaptureEndpoints
{
    /// <summary>Maps opt-in authenticated loopback-only controls; registration alone never pauses a source.</summary>
    public static void MapNativeSessionCaptureBoundary(this WebApplication app)
    {
        app.MapNativeCheckpointBoundary();
        bool Allowed(HttpContext request)
        {
            if (!app.Environment.IsDevelopment() || !app.Configuration.GetValue<bool>("SessionCapture:Enabled")
                || request.Connection.RemoteIpAddress is not { } remote || !IPAddress.IsLoopback(remote)) return false;
            var configured = app.Configuration["SessionCapture:AccessToken"];
            var supplied = request.Request.Headers["X-IFM-Session-Capture"].ToString();
            return configured is { Length: >= 32 and <=256 } && supplied.Length is >=32 and <=256
                && CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(configured)), SHA256.HashData(Encoding.UTF8.GetBytes(supplied)));
        }
        static ulong Lease(string value) => ulong.TryParse(value, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var lease) && lease != 0
            ? lease : throw new ArgumentException("A nonzero hexadecimal lease is required.");
        app.MapGet("/internal/session-capture/native/status", IResult (HttpContext request) =>
        {
            if(!Allowed(request))return Results.NotFound();
            var backend=request.RequestServices.GetRequiredService<NativeDatasetBackend>();
            return Results.Ok(new{Version=1,Kind="NativeForwardingOnly",CrossOwnerSnapshot=false,Status=backend.Runtime.SessionForwardingStatus(),Production=backend.Runtime.SessionProductionStatus(),
                RawRetentionLease=request.RequestServices.GetRequiredService<TomasAI.IFM.Application.MarketData.Storage.RawFuturesStoragePublisher>().SnapshotRetentionLease});
        });
        app.MapPost("/internal/session-capture/native/pause-production/{lease}", async Task<IResult> (HttpContext request,string lease) =>
        {
            if(!Allowed(request))return Results.NotFound();
            try
            {
                var held=Lease(lease);var backend=request.RequestServices.GetRequiredService<NativeDatasetBackend>();
                await Task.Run(()=>backend.Runtime.PauseSourceProduction(app.Configuration["MarketDataHost:Scope"]??"development",held,TimeSpan.FromSeconds(10))).ConfigureAwait(false);
                var raw=request.RequestServices.GetRequiredService<TomasAI.IFM.Application.MarketData.Storage.RawFuturesStoragePublisher>();raw.HoldSnapshotRetention(held);
                return Results.Ok(new{Version=1,Kind="NativeProductionPausedRelayRetained",Qualified=false,CrossOwnerSnapshot=false,Status=backend.Runtime.SessionProductionStatus(),RawRetentionLease=raw.SnapshotRetentionLease});
            }
            catch(Exception error)when(error is InvalidOperationException or ArgumentException or NotSupportedException)
            {return Results.Conflict(new{error=error.GetType().Name,qualified=false});}
        });
        app.MapPost("/internal/session-capture/native/resume-production/{lease}",Task<IResult> (HttpContext request,string lease)=>
        {
            if(!Allowed(request))return Task.FromResult<IResult>(Results.NotFound());
            try
            {
                var held=Lease(lease);var backend=request.RequestServices.GetRequiredService<NativeDatasetBackend>();
                var raw=request.RequestServices.GetRequiredService<TomasAI.IFM.Application.MarketData.Storage.RawFuturesStoragePublisher>();
                if(raw.SnapshotRetentionLease!=held)throw new InvalidOperationException("Native production resume requires its matching raw snapshot retention.");
                backend.Runtime.ResumeSourceProduction(held);raw.ReleaseSnapshotRetention(held);
                return Task.FromResult<IResult>(Results.Ok(new{Kind="NativeProductionResumed",Qualified=false,CrossOwnerSnapshot=false,Status=backend.Runtime.SessionProductionStatus()}));
            }
            catch(Exception error)when(error is InvalidOperationException or ArgumentException or NotSupportedException)
            {return Task.FromResult<IResult>(Results.Conflict(new{error=error.GetType().Name,qualified=false}));}
        });
        app.MapPost("/internal/session-capture/native/hold/{lease}", async (HttpContext request,string lease) =>
        {
            if (!Allowed(request)) return Results.NotFound();
            try
            {
                var held=Lease(lease);var backend=request.RequestServices.GetRequiredService<NativeDatasetBackend>();
                await Task.Run(() => backend.Runtime.HoldSourceForwarding(held, TimeSpan.FromSeconds(10))).ConfigureAwait(false);
                return Results.Ok(new { Kind = "NativeForwardingOnly", CrossOwnerSnapshot = false, Status = backend.Runtime.SessionForwardingStatus() });
            }
            catch (Exception error) when (error is InvalidOperationException or ArgumentException or NotSupportedException)
            { return Results.Conflict(new { Error = error.GetType().Name, Detail = "Native forwarding hold was not established; Core-only operation is refused." }); }
        });
        app.MapPost("/internal/session-capture/native/release/{lease}", (HttpContext request,string lease) =>
        {
            if (!Allowed(request)) return Results.NotFound();
            try { var held=Lease(lease);var backend=request.RequestServices.GetRequiredService<NativeDatasetBackend>();backend.Runtime.ReleaseSourceForwarding(held); return Results.Ok(backend.Runtime.SessionForwardingStatus()); }
            catch (Exception error) when (error is InvalidOperationException or ArgumentException or NotSupportedException)
            { return Results.Conflict(new { Error = error.GetType().Name }); }
        });
        app.MapGet("/internal/session-capture/native/prefix/{lease}", (HttpContext request,string lease) =>
        {
            if (!Allowed(request)) return Results.NotFound();
            try { var held=Lease(lease);var backend=request.RequestServices.GetRequiredService<NativeDatasetBackend>();return Results.Text(backend.Runtime.CaptureSourcePrefixManifest(app.Configuration["MarketDataHost:Scope"] ?? "development",held), "application/json"); }
            catch (Exception error) when (error is InvalidOperationException or ArgumentException or NotSupportedException)
            { return Results.Conflict(new { Error = error.GetType().Name }); }
        });
    }
}
