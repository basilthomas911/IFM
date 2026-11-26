using System.Security.Cryptography;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
namespace TomasAI.IFM.Framework.Messaging.Nats.UnitTests;

public sealed class RealtimeSessionArchiveBoundaryTests
{
    static RealtimeSessionArchiveRecord Record(int id) => new(){Kind="Fixture",LogicalSourceKey=id.ToString(),Subject="fixture.original",OriginalWire=new byte[]{1,(byte)id}};
    static string HashPrefix(string path,long length){using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);var body=new byte[checked((int)length)];stream.ReadExactly(body);return Convert.ToHexStringLower(SHA256.HashData(body));}
    static RealtimeSessionArchiveBoundary Floor(RealtimeSessionArchivePrefix prefix,string session)=>new(1,"FrozenOwnerSourceArchiveFloor",session,prefix.Protocol,"fixture-boundary","held-api-owner","source-outbox",new('1',64),new('2',64),prefix.Records,
        prefix.Files.Select(file=>new RealtimeSessionArchiveBoundaryFile(file.RelativePath,file.Length,HashPrefix(Path.Combine(prefix.SessionDirectory,file.RelativePath),file.Length))).ToArray());
    [Fact]
    public async Task Actual_initial_committed_prefix_excludes_already_owned_records_and_allows_authentic_appends()
    {
        var root=Path.Combine(Path.GetTempPath(),"ifm-archive-floor-"+Guid.NewGuid().ToString("N"));const string session="original-session";
        try
        {
            await using var archive=new DurableRealtimeSessionArchive(new(){Enabled=true,Directory=root,SessionId=session});
            await archive.RecordAsync(Record(1));await archive.RecordAsync(Record(2));var floor=Floor(await archive.GetCommittedPrefixAsync(),session);
            Assert.Equal(2,RealtimeSessionArchiveBoundaryVerifier.VerifyInitial(archive.SessionDirectory,floor));
            await archive.RecordAsync(Record(3));Assert.Equal(2,RealtimeSessionArchiveBoundaryVerifier.VerifyInitial(archive.SessionDirectory,floor));
            var replay=archive.ReadCommitted().Where(entry=>entry.Ordinal>floor.LastCommittedOrdinal).ToArray();Assert.Single(replay);Assert.Equal("3",replay[0].Record.LogicalSourceKey);
            var full=Floor(await archive.GetCommittedPrefixAsync(),session);Assert.Equal(3,RealtimeSessionArchiveBoundaryVerifier.VerifyInitial(archive.SessionDirectory,full));
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
    [Fact]
    public async Task Asserted_ordinal_foreign_session_duplicate_path_or_partial_commit_cannot_replace_exact_offsets()
    {
        var root=Path.Combine(Path.GetTempPath(),"ifm-archive-floor-"+Guid.NewGuid().ToString("N"));const string session="original-session";
        try
        {
            await using var archive=new DurableRealtimeSessionArchive(new(){Enabled=true,Directory=root,SessionId=session});await archive.RecordAsync(Record(1));var floor=Floor(await archive.GetCommittedPrefixAsync(),session);
            Assert.Throws<InvalidDataException>(()=>RealtimeSessionArchiveBoundaryVerifier.VerifyInitial(archive.SessionDirectory,floor with{LastCommittedOrdinal=0}));
            Assert.Throws<InvalidDataException>(()=>RealtimeSessionArchiveBoundaryVerifier.VerifyInitial(archive.SessionDirectory,floor with{ArchiveSessionId="another-session"}));
            Assert.Throws<InvalidDataException>(()=>RealtimeSessionArchiveBoundaryVerifier.VerifyInitial(archive.SessionDirectory,floor with{Files=[..floor.Files,floor.Files[0]]}));
            var segment=floor.Files.First(file=>file.RelativePath.EndsWith(".rto",StringComparison.Ordinal)&&file.Length>0);var shortened=segment with{Length=segment.Length-1,Sha256=HashPrefix(Path.Combine(archive.SessionDirectory,segment.RelativePath),segment.Length-1)};
            Assert.Throws<InvalidDataException>(()=>RealtimeSessionArchiveBoundaryVerifier.VerifyInitial(archive.SessionDirectory,floor with{Files=floor.Files.Select(file=>file==segment?shortened:file).ToArray()}));
            Assert.Throws<InvalidDataException>(()=>RealtimeSessionArchiveBoundaryVerifier.VerifyInitial(archive.SessionDirectory,floor with{Files=floor.Files.Select(file=>file==segment?file with{Sha256=new('a',64)}:file).ToArray()}));
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
