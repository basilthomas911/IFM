using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
namespace TomasAI.IFM.Application.MarketData.UnitTests;
public sealed class CertifiedReplayArchiveInputsTests
{
    static string Hash(string path){using var file=File.OpenRead(path);return Convert.ToHexStringLower(SHA256.HashData(file));}
    [Fact]
    public async Task Certified_shared_copy_reads_original_records_without_a_third_body_copy_and_detects_changed_input()
    {
        var root=Path.Combine(Path.GetTempPath(),"ifm-shared-archive-"+Guid.NewGuid().ToString("N"));const string session="shared-real-component";
        try
        {
            RealtimeSessionArchivePrefix prefix;var live=Path.Combine(root,"original");
            await using(var archive=new DurableRealtimeSessionArchive(new(){Enabled=true,Directory=live,SessionId=session}))
            {await archive.RecordAsync(new(){Kind="Fixture",LogicalSourceKey="original",Subject="original",OriginalWire=new byte[]{1,2,3}});prefix=await archive.GetCommittedPrefixAsync();}
            var copy=Path.Combine(root,"copied");var epoch="archive_"+Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(session)))[..24];var target=Path.Combine(copy,epoch);Directory.CreateDirectory(target);
            var files=prefix.Files.Select(file=>{var destination=Path.Combine(target,file.RelativePath);Directory.CreateDirectory(Path.GetDirectoryName(destination)!);File.Copy(Path.Combine(prefix.SessionDirectory,file.RelativePath),destination);return new{relativePath=file.RelativePath,bytes=file.Length,sha256=Hash(destination)};}).ToArray();
            var manifest=Path.Combine(target,"archive-prefix-copy.json");await File.WriteAllTextAsync(manifest,JsonSerializer.Serialize(new{kind="CommittedSourceArchivePrefixes",archiveOwnershipOnly=true,boundary=prefix,files}));var manifestSha=Hash(manifest);
            CertifiedReplayArchiveInputs.Prepare(copy,session,manifest,manifestSha,Path.Combine(root,"restored"),true);
            await using(var reader=new DurableRealtimeSessionArchive(new(){Enabled=true,Directory=copy,SessionId=session}))
            {Assert.Single(reader.ReadCommitted());CertifiedReplayArchiveInputs.Prepare(copy,session,manifest,manifestSha,Path.Combine(root,"restored"),true);}
            Assert.False(Directory.Exists(Path.Combine(root,"restored")));
            Assert.Throws<InvalidDataException>(()=>CertifiedReplayArchiveInputs.Prepare(live,session,manifest,manifestSha,root,true));
            var body=files.First(file=>file.relativePath.EndsWith(".rto",StringComparison.Ordinal));using(var stream=new FileStream(Path.Combine(target,body.relativePath),FileMode.Open,FileAccess.ReadWrite)){stream.Position=stream.Length-1;var value=stream.ReadByte();stream.Position=stream.Length-1;stream.WriteByte((byte)(value^1));}
            Assert.Throws<InvalidDataException>(()=>CertifiedReplayArchiveInputs.Prepare(copy,session,manifest,manifestSha,root,true));
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
