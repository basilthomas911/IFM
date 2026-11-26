using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Workers;

namespace TomasAI.IFM.Application.MarketData.Rust.Host;

/// <summary>Explicit local checkpoint tools; no route activates a provider or releases the caller's source fence.</summary>
public static class NativeCheckpointEndpoints
{
    public static void MapNativeCheckpointBoundary(this WebApplication app)
    {
        app.MapPost("/internal/session-capture/native/checkpoint/export/{lease}/{boundary}",async (HttpContext request,string lease,string boundary)=>
        {
            if(!Allowed(app,request))return Results.NotFound();
            var held=0UL;var frozen=false;NativeDatasetBackend? backend=null;
            try
            {
                if(!ulong.TryParse(lease,NumberStyles.AllowHexSpecifier,CultureInfo.InvariantCulture,out held)||held==0)throw new ArgumentException("Nonzero source lease required.");
                var directory=BoundaryDirectory(app,boundary);if(Directory.Exists(directory))throw new IOException("Checkpoint boundary already exists.");
                backend=request.RequestServices.GetRequiredService<NativeDatasetBackend>();
                var production=backend.Runtime.SessionProductionStatus();
                if(!production.Supported||production.Lease!=held||production.ActiveCallbacks!=0)
                    throw new InvalidOperationException("Native owner checkpoint requires the matching drained source-production pause.");
                if(request.RequestServices.GetRequiredService<TomasAI.IFM.Application.MarketData.Storage.RawFuturesStoragePublisher>().SnapshotRetentionLease!=held)
                    throw new InvalidOperationException("Native owner checkpoint requires matching raw journal retention through shutdown.");
                var api=request.RequestServices.GetRequiredService<IMarketDataApi>();
                Directory.CreateDirectory(directory);
                var scope=app.Configuration["MarketDataHost:Scope"]??"development";
                backend.Runtime.BeginNativeCheckpoint(scope,held);frozen=true;
                using var budget=CancellationTokenSource.CreateLinkedTokenSource(request.RequestAborted);budget.CancelAfter(TimeSpan.FromSeconds(20));
                var snapshot=await api.GetBackendReadSnapshotAsync(budget.Token).ConfigureAwait(false);
                var binary=MarketDataHostConnection.Encode(snapshot);
                if(binary.Length>16*1024*1024)throw new InvalidDataException("Original backend snapshot exceeds its bound.");
                WriteNew(Path.Combine(directory,"backend-snapshot.bin"),binary);
                var sourcePrefix=Encoding.UTF8.GetBytes(backend.Runtime.CaptureSourcePrefixManifest(scope,held));
                if(sourcePrefix.Length>1024*1024)throw new InvalidDataException("Original native source prefix exceeds its metadata bound.");
                WriteNew(Path.Combine(directory,"source-prefix.json"),sourcePrefix);
                var checkpoint=backend.Runtime.ExportNativeCheckpoint(scope,held,Path.Combine(directory,"native.checkpoint"));
                var certificate=new NativeCheckpointCertificate(1,"FrozenNativeAndBackendSnapshot",boundary,scope,held,
                    "native.checkpoint",checkpoint.Bytes,checkpoint.Sha256,"backend-snapshot.bin",binary.Length,Convert.ToHexString(SHA256.HashData(binary)).ToLowerInvariant(),false,false,
                    "source-prefix.json",sourcePrefix.Length,Convert.ToHexString(SHA256.HashData(sourcePrefix)).ToLowerInvariant());
                WriteNew(Path.Combine(directory,"checkpoint.json"),JsonSerializer.SerializeToUtf8Bytes(certificate));
                return Results.Json(certificate);
            }
            catch(Exception error)when(error is ArgumentException or InvalidOperationException or NotSupportedException or IOException or OperationCanceledException)
            {return Results.Conflict(new{error=error.GetType().Name,qualified=false,boundaryComplete=false});}
            finally{if(frozen)backend!.Runtime.EndNativeCheckpoint(held);}
        });
        app.MapPost("/internal/session-capture/native/checkpoint/import/{boundary}",(HttpContext request,string boundary)=>
        {
            if(!Allowed(app,request)||!app.Environment.IsEnvironment("Test")||!app.Configuration.GetValue<bool>("SessionCapture:AllowCheckpointImport"))return Results.NotFound();
            try
            {
                var directory=BoundaryDirectory(app,boundary);var file=Path.Combine(directory,"checkpoint.json");
                if(new FileInfo(file).Length>65536)throw new InvalidDataException("Checkpoint certificate exceeds bounds.");
                var certificate=JsonSerializer.Deserialize<NativeCheckpointCertificate>(File.ReadAllBytes(file))??throw new InvalidDataException("Missing checkpoint certificate.");
                if(certificate.Version!=1||certificate.Kind!="FrozenNativeAndBackendSnapshot"||certificate.Boundary!=boundary||certificate.Qualified||certificate.CrossOwnerSnapshot||certificate.CheckpointFile!="native.checkpoint"||certificate.BackendSnapshotFile!="backend-snapshot.bin")throw new InvalidDataException("Unknown native boundary certificate.");
                var snapshot=Path.Combine(directory,certificate.BackendSnapshotFile);
                using(var input=File.OpenRead(snapshot))if(input.Length!=certificate.BackendSnapshotBytes||Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant()!=certificate.BackendSnapshotSha256)throw new InvalidDataException("Original backend snapshot changed.");
                if(certificate.SourcePrefixFile!="source-prefix.json"||certificate.SourcePrefixBytes is not (>0 and <=1024*1024)||certificate.SourcePrefixSha256 is null)throw new InvalidDataException("Original native source prefix is absent.");
                using(var input=File.OpenRead(Path.Combine(directory,certificate.SourcePrefixFile)))if(input.Length!=certificate.SourcePrefixBytes||Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant()!=certificate.SourcePrefixSha256)throw new InvalidDataException("Original native source prefix changed.");
                var backend=request.RequestServices.GetRequiredService<NativeDatasetBackend>();
                var restored=backend.Runtime.ImportNativeCheckpoint(certificate.Scope,Path.Combine(directory,certificate.CheckpointFile),certificate.CheckpointSha256);
                if(restored.Bytes!=certificate.CheckpointBytes)throw new InvalidDataException("Native checkpoint length changed.");
                return Results.Json(new{state="ImportedStagedNativeImage",qualified=false,providerFactoriesRestored=false,certificate.BackendSnapshotSha256,restored.Sha256});
            }
            catch(Exception error)when(error is ArgumentException or InvalidOperationException or NotSupportedException or IOException or JsonException)
            {return Results.Conflict(new{error=error.GetType().Name,qualified=false});}
        });
    }
    static bool Allowed(WebApplication app,HttpContext request)
    {
        if(!(app.Environment.IsDevelopment()||app.Environment.IsEnvironment("Test"))||!app.Configuration.GetValue<bool>("SessionCapture:Enabled")||request.Connection.RemoteIpAddress is not{} remote||!IPAddress.IsLoopback(remote))return false;
        var expected=app.Configuration["SessionCapture:AccessToken"];var actual=request.Request.Headers["X-IFM-Session-Capture"].ToString();
        return expected is{Length:>=32 and <=256}&&actual.Length<=256&&CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(expected)),SHA256.HashData(Encoding.UTF8.GetBytes(actual)));
    }
    static string BoundaryDirectory(WebApplication app,string boundary)
    {
        if(!Regex.IsMatch(boundary,"^[a-f0-9]{12}$"))throw new ArgumentException("A bounded boundary identity is required.");
        var root=app.Configuration["SessionCapture:CheckpointDirectory"];
        if(string.IsNullOrWhiteSpace(root)||!Path.IsPathFullyQualified(root)||!Directory.Exists(root))throw new InvalidOperationException("An explicit existing checkpoint directory is required.");
        var path=Path.Combine(Path.GetFullPath(root),boundary);
        for(var current=path;current is not null;current=Path.GetDirectoryName(current))
            if((File.Exists(current)||Directory.Exists(current))&&(File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new InvalidOperationException("Checkpoint paths cannot traverse filesystem links.");
        return path;
    }
    static void WriteNew(string path,ReadOnlySpan<byte> bytes)
    {using var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);output.Write(bytes);output.Flush(true);}
}

public sealed record NativeCheckpointCertificate(int Version,string Kind,string Boundary,string Scope,ulong SourceLease,
    string CheckpointFile,long CheckpointBytes,string CheckpointSha256,string BackendSnapshotFile,long BackendSnapshotBytes,string BackendSnapshotSha256,
    bool Qualified,bool CrossOwnerSnapshot,string? SourcePrefixFile=null,long? SourcePrefixBytes=null,string? SourcePrefixSha256=null);
