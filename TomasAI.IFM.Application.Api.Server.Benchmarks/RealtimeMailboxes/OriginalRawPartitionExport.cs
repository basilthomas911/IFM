using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Remote;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;

/// <summary>Offline exact typed source inventory; no connection to a database or provider is opened.</summary>
public static class OriginalRawPartitionExport
{
    static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web){WriteIndented=true,PropertyNameCaseInsensitive=true};
    public sealed record Configuration(int Version,string ArchiveCopyManifest,string ArchiveCopyManifestSha256,string ArchiveSessionId,
        string NativeCheckpointCertificate,string NativeCheckpointCertificateSha256,string InitialBackendSnapshot,string InitialBackendSnapshotSha256,
        string OutputPath,long MaximumRecords=100000000,int MaximumPartitions=100000);
    public static async Task RunAsync(string path)
    {
        if(new FileInfo(path).Length>1024*1024)throw new InvalidDataException("Raw partition configuration exceeds bound.");
        var config=JsonSerializer.Deserialize<Configuration>(File.ReadAllBytes(path),Json)??throw new InvalidDataException("Missing raw partition configuration.");
        if(config.Version!=1||config.MaximumRecords is <1 or >100000000||config.MaximumPartitions is <1 or >1000000)throw new InvalidDataException("Invalid original source inventory bounds.");
        Verify(config.ArchiveCopyManifest,config.ArchiveCopyManifestSha256);Verify(config.NativeCheckpointCertificate,config.NativeCheckpointCertificateSha256);Verify(config.InitialBackendSnapshot,config.InitialBackendSnapshotSha256);
        using var native=JsonDocument.Parse(File.ReadAllBytes(config.NativeCheckpointCertificate));
        if(native.RootElement.GetProperty("Kind").GetString()!="FrozenNativeAndBackendSnapshot"||native.RootElement.GetProperty("BackendSnapshotSha256").GetString()!=config.InitialBackendSnapshotSha256)throw new InvalidDataException("Original baseline is not bound to its native checkpoint.");
        using var document=JsonDocument.Parse(File.ReadAllBytes(config.ArchiveCopyManifest));var manifest=document.RootElement;
        if(manifest.GetProperty("kind").GetString()!="CommittedSourceArchivePrefixes"||!manifest.GetProperty("archiveOwnershipOnly").GetBoolean())throw new InvalidDataException("Original source archive copy is required.");
        var epoch=Path.GetDirectoryName(Path.GetFullPath(config.ArchiveCopyManifest))!;
        var expected="archive_"+Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(config.ArchiveSessionId)))[..24];
        if(Path.GetFileName(epoch)!=expected)throw new InvalidDataException("Archive copy must use its original epoch directory; no second full copy is created.");
        var prefix=manifest.GetProperty("boundary").Deserialize<RealtimeSessionArchivePrefix>(Json)??throw new InvalidDataException("Missing original source prefix.");
        var copied=manifest.GetProperty("files").EnumerateArray().Select(file=>new RealtimeSessionArchiveBoundaryFile(file.GetProperty("relativePath").GetString()!,file.GetProperty("bytes").GetInt64(),file.GetProperty("sha256").GetString()!)).ToArray();
        var original=prefix.Files.ToDictionary(file=>file.RelativePath,file=>file.Length,StringComparer.Ordinal);
        if(copied.Length!=original.Count||copied.Any(file=>!original.TryGetValue(file.RelativePath,out var length)||length!=file.Length))throw new InvalidDataException("Copied source offsets differ from the committed original prefix.");
        RealtimeSessionArchiveBoundaryVerifier.VerifyPrefix(epoch,config.ArchiveSessionId,prefix.Protocol,prefix.Records,copied);
        var inventory=new OutcomeRawPartitions(config.MaximumPartitions);inventory.AddBaseline(MarketDataHostConnection.Decode<MarketDataReadSnapshot>(File.ReadAllBytes(config.InitialBackendSnapshot)));
        long count=0,decoded=0;
        await using(var archive=new DurableRealtimeSessionArchive(new(){Enabled=true,Directory=Path.GetDirectoryName(epoch)!,SessionId=config.ArchiveSessionId,MaximumArchiveBytes=128L*1024*1024*1024,MaximumReceiptBytes=16L*1024*1024*1024}))
            foreach(var entry in archive.ReadCommitted())
            {
                if(++count>config.MaximumRecords)throw new InvalidDataException("Original source record bound exceeded.");
                if(inventory.AddRecord(entry.Record))decoded++;
            }
        if(count!=prefix.Records)throw new InvalidDataException("Original source count changed while inspecting.");
        inventory.CompleteBaselineDates();if(inventory.Partitions.Count==0)throw new InvalidDataException("No original raw partition identities were available.");var output=new {version=1,kind="OriginalRawPartitionManifest",sourceArchiveSha256=config.ArchiveCopyManifestSha256,nativeCheckpointSha256=config.NativeCheckpointCertificateSha256,initialBackendSnapshotSha256=config.InitialBackendSnapshotSha256,archiveSessionId=config.ArchiveSessionId,records=count,decodedRawRecords=decoded,partitions=inventory.Partitions,qualified=false,
            scope="All committed original archive entries including pre-floor inputs and complete initial manifests/current values; safe known legacy table overcoverage."};
        using var stream=new FileStream(config.OutputPath,FileMode.CreateNew,FileAccess.Write,FileShare.Read);await JsonSerializer.SerializeAsync(stream,output,Json);stream.Flush(true);
    }
    static void Verify(string path,string hash)
    {using var stream=File.OpenRead(path);if(!string.Equals(Convert.ToHexStringLower(SHA256.HashData(stream)),hash,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Original raw inventory input hash differs.");}
}
