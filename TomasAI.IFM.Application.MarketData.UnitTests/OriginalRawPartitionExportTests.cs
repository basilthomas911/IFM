using System.Security.Cryptography;
using System.Text.Json;
using MessagePack;
using TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Application.MarketData.UnitTests;
public sealed class OriginalRawPartitionExportTests
{
    static string Hash(string path){using var stream=File.OpenRead(path);return Convert.ToHexStringLower(SHA256.HashData(stream));}
    [Fact]
    public async Task Actual_archive_prefix_produces_hash_bound_typed_partitions_and_refuses_tampered_body()
    {
        var root=Path.Combine(Path.GetTempPath(),"ifm-raw-partition-export-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            const string session="actual-small-archive";RealtimeSessionArchivePrefix prefix;
            await using(var archive=new DurableRealtimeSessionArchive(new(){Enabled=true,Directory=root,SessionId=session}))
            {
                var date=new DateOnly(2026,11,30);var trade=new FuturesTickTradeDataChangedEvent{AssetTypeId=AssetTypeId.Futures,TickDataId=new("ES20261218",date,1,new DateTime(2026,11,30,14,30,0,DateTimeKind.Utc))};
                var batch=new RawFuturesStorageBatch(Guid.NewGuid(),100,[new(1,Guid.NewGuid(),1,MessagePackSerializer.Serialize(trade))]);
                await archive.RecordAsync(new(){Kind="RawFuturesStorage",LogicalSourceKey="original-raw-frame",Subject="original.raw",OriginalWire=MessagePackSerializer.Serialize(batch)});prefix=await archive.GetCommittedPrefixAsync();
            }
            var json=new JsonSerializerOptions(JsonSerializerDefaults.Web);var copy=Path.Combine(prefix.SessionDirectory,"archive-prefix-copy.json");
            var files=prefix.Files.Select(file=>new{relativePath=file.RelativePath,bytes=file.Length,sha256=Hash(Path.Combine(prefix.SessionDirectory,file.RelativePath))}).ToArray();
            File.WriteAllBytes(copy,JsonSerializer.SerializeToUtf8Bytes(new{kind="CommittedSourceArchivePrefixes",archiveOwnershipOnly=true,boundary=prefix,files},json));
            var baseline=Path.Combine(root,"backend.bin");File.WriteAllBytes(baseline,MarketDataHostConnection.Encode(new MarketDataReadSnapshot(new(true,new(2026,11,30),[]),[],[],[])));
            var certificate=Path.Combine(root,"checkpoint.json");File.WriteAllBytes(certificate,JsonSerializer.SerializeToUtf8Bytes(new{Kind="FrozenNativeAndBackendSnapshot",BackendSnapshotSha256=Hash(baseline)}));
            var output=Path.Combine(root,"partitions.json");var config=new OriginalRawPartitionExport.Configuration(1,copy,Hash(copy),session,certificate,Hash(certificate),baseline,Hash(baseline),output);
            var configPath=Path.Combine(root,"config.json");File.WriteAllBytes(configPath,JsonSerializer.SerializeToUtf8Bytes(config,json));await OriginalRawPartitionExport.RunAsync(configPath);
            using(var actual=JsonDocument.Parse(File.ReadAllBytes(output))){Assert.Equal(4,actual.RootElement.GetProperty("partitions").GetArrayLength());Assert.Equal(config.ArchiveCopyManifestSha256,actual.RootElement.GetProperty("sourceArchiveSha256").GetString());Assert.False(actual.RootElement.GetProperty("qualified").GetBoolean());}
            var segment=prefix.Files.First(file=>file.RelativePath.EndsWith(".rto",StringComparison.Ordinal)&&file.Length>0);var segmentPath=Path.Combine(prefix.SessionDirectory,segment.RelativePath);var corrupt=File.ReadAllBytes(segmentPath);corrupt[40]^=1;File.WriteAllBytes(segmentPath,corrupt);
            File.WriteAllBytes(configPath,JsonSerializer.SerializeToUtf8Bytes(config with{OutputPath=Path.Combine(root,"must-not-exist.json")},json));await Assert.ThrowsAsync<InvalidDataException>(()=>OriginalRawPartitionExport.RunAsync(configPath));Assert.False(File.Exists(Path.Combine(root,"must-not-exist.json")));
        }
        finally{Directory.Delete(root,true);}
    }
}
