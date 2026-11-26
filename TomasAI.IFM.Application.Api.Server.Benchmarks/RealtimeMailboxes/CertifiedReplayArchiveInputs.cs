using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;
/// <summary>Hash-certified copied input ownership. Shared mode never records or appends an authoritative source.</summary>
public static class CertifiedReplayArchiveInputs
{
    static readonly JsonSerializerOptions Json=new(){PropertyNameCaseInsensitive=true};
    static string Child(string root,string relative)
    {
        var result=Path.GetFullPath(Path.Combine(root,relative));var rel=Path.GetRelativePath(root,result);
        if(Path.IsPathRooted(rel)||rel==".."||rel.StartsWith(".."+Path.DirectorySeparatorChar,StringComparison.Ordinal))throw new InvalidDataException("Archive input escapes its root.");
        for(var current=result;current is not null;current=Path.GetDirectoryName(current))if((File.Exists(current)||Directory.Exists(current))&&(File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Archive inputs cannot traverse links.");
        return result;
    }
    static void Verify(string path,string sha){using var stream=File.OpenRead(path);if(!string.Equals(Convert.ToHexString(SHA256.HashData(stream)),sha,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Original archive input changed.");}
    public static void Prepare(string directory,string sessionId,string manifestPath,string manifestSha256,string restoreRoot,bool shared)
    {
        Verify(manifestPath,manifestSha256);if(new FileInfo(manifestPath).Length>16*1024*1024)throw new InvalidDataException("Archive manifest exceeds its metadata bound.");
        using var document=JsonDocument.Parse(File.ReadAllBytes(manifestPath));var manifest=document.RootElement;
        if(manifest.GetProperty("kind").GetString()!="CommittedSourceArchivePrefixes"||!manifest.GetProperty("archiveOwnershipOnly").GetBoolean())throw new InvalidDataException("Original source ownership is required.");
        var sourceRoot=Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;var destination=Path.GetFullPath(directory);var epoch="archive_"+Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sessionId)))[..24];
        var prefix=manifest.GetProperty("boundary").Deserialize<RealtimeSessionArchivePrefix>(Json)??throw new InvalidDataException("Original prefix metadata is missing.");
        if(shared)
        {
            if(!string.Equals(Child(destination,epoch),sourceRoot,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Shared input must be exactly the certified copied epoch directory.");
            var live=Path.GetFullPath(prefix.SessionDirectory);
            static bool Within(string parent,string child)=>string.Equals(parent,child,StringComparison.OrdinalIgnoreCase)||child.StartsWith(parent.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
            if(Within(live,destination)||Within(destination,live))throw new InvalidDataException("A live/original recorder cannot be opened as shared replay input.");
        }
        else if(!destination.StartsWith(Path.GetFullPath(restoreRoot)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||Directory.Exists(destination))throw new InvalidDataException("Private archive copy must be new and inside the restored owner root.");
        var expected=prefix.Files.ToDictionary(x=>x.RelativePath.Replace('\\','/'),x=>x.Length,StringComparer.OrdinalIgnoreCase);var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var verified=new List<RealtimeSessionArchiveBoundaryFile>();long total=0;
        foreach(var file in manifest.GetProperty("files").EnumerateArray())
        {
            var relative=file.GetProperty("relativePath").GetString()!;var length=file.GetProperty("bytes").GetInt64();var sha=file.GetProperty("sha256").GetString()!;
            if(!seen.Add(relative)||!expected.TryGetValue(relative.Replace('\\','/'),out var original)||original!=length||length<0)throw new InvalidDataException("Original archive copy offsets differ.");
            total=checked(total+length);if(total>150L*1024*1024*1024)throw new InvalidDataException("Original archive exceeds the qualified finite input bound.");
            var source=Child(sourceRoot,relative);if(new FileInfo(source).Length!=length)throw new InvalidDataException("Original prefix length changed.");Verify(source,sha);
            if(!shared){var target=Child(Path.Combine(destination,epoch),relative);Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(source,target,false);Verify(target,sha);}
            verified.Add(new(relative,length,sha));
        }
        if(seen.Count!=expected.Count)throw new InvalidDataException("Original prefix copy is incomplete.");
        RealtimeSessionArchiveBoundaryVerifier.VerifyPrefix(Path.Combine(destination,epoch),sessionId,prefix.Protocol,prefix.Records,verified);
    }
}
