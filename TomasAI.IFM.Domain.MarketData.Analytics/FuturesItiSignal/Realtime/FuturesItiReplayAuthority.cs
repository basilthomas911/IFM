using MessagePack;
using System.Security.Cryptography;
namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime;

[MessagePackObject]
public sealed record FuturesItiReplayProgress([property:Key(0)]string Contract,[property:Key(1)]Guid Recovery,[property:Key(2)]long Batch,[property:Key(3)]Guid Epoch,[property:Key(4)]Guid SourceId,[property:Key(5)]byte[] SourceSha256);
[MessagePackObject]
public sealed record FuturesItiReplayAuthoritySnapshot([property:Key(0)]FuturesItiReplayProgress[] Progress,[property:Key(1)]Guid[] VerifiedEpochs);
/// <summary>Original replay progression survives restart; completed source receipts alone cannot reconstruct a partial replay.</summary>
public sealed class FuturesItiReplayAuthority(string directory,Guid root)
{
    string PathName=>Path.Combine(directory,$"{root:N}.replay-authority");
    public FuturesItiReplayAuthoritySnapshot Restore()
    {
        if(!File.Exists(PathName))return new([],[]);
        var bytes=File.ReadAllBytes(PathName);
        if(bytes.Length is <33 or >1024*1024||!SHA256.HashData(bytes.AsSpan(32)).AsSpan().SequenceEqual(bytes.AsSpan(0,32)))throw new InvalidDataException("ITI replay authority has a gap or changed bytes.");
        var snapshot=MessagePackSerializer.Deserialize<FuturesItiReplayAuthoritySnapshot>(bytes.AsMemory(32));
        if(snapshot.Progress.Length>1024||snapshot.VerifiedEpochs.Length>65536)throw new InvalidDataException("ITI replay authority exceeds its bound.");
        return snapshot;
    }
    public void Save(FuturesItiReplayAuthoritySnapshot snapshot)
    {
        Directory.CreateDirectory(directory);var body=MessagePackSerializer.Serialize(snapshot);
        if(body.Length>1024*1024)throw new IOException("ITI replay authority capacity exhausted.");
        var temporary=PathName+".pending";
        using(var file=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None)){file.Write(SHA256.HashData(body));file.Write(body);file.Flush(true);}
        File.Move(temporary,PathName,true);
    }
}
