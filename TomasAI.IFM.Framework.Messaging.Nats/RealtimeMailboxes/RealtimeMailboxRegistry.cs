using System.Text;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;

/// <summary>Protocol2 recovers required partitions from already durable frozen source manifests, without another per-partition commit.</summary>
internal static class RealtimeMailboxRegistry
{
    internal static ActorThreadId[] Recover(string directory,IEnumerable<CapturedRealtimeSource> pending,int maximumMailboxes)
    {
        var identities=new Dictionary<string,ActorThreadId>(StringComparer.Ordinal);
        void Add(ActorThreadId identity,string? expectedKey=null)
        {
            if(identity.ActorType!=ActorType.Realtime||string.IsNullOrWhiteSpace(identity.Name)||string.IsNullOrWhiteSpace(identity.EntityId)
                ||identity.Name.Length>256||identity.EntityId.Length>16384)throw new InvalidDataException("Invalid realtime registry identity.");
            var key=RealtimeMailboxCodec.MailboxKey(identity);
            if(expectedKey is not null&&key!=expectedKey)throw new InvalidDataException("Realtime mailbox registry identity mismatch.");
            if(identities.TryGetValue(key,out var prior))
            {
                if(prior!=identity)throw new InvalidDataException("Realtime mailbox registry digest/identity conflict.");
                return;
            }
            if(identities.Count>=maximumMailboxes)throw new IOException("Recovered realtime mailbox registry exceeds its configured capacity.");
            identities.Add(key,identity);
        }
        // Old version2 files stay authoritative and byte-compatible. They are
        // validated, never silently removed or rewritten into a weaker format.
        if(Directory.Exists(directory))foreach(var path in Directory.EnumerateFiles(directory,"*.rmb").Order(StringComparer.Ordinal))
        {
            using var stream=File.OpenRead(path);
            if(stream.Length is <8 or >128*1024)throw new InvalidDataException("Invalid realtime legacy registry frame length.");
            using var reader=new BinaryReader(stream,Encoding.UTF8,true);
            if(reader.ReadInt32()!=2)throw new InvalidDataException("Unknown realtime registry version.");
            var identity=new ActorThreadId((ActorType)reader.ReadInt32(),reader.ReadString(),reader.ReadString());
            if(stream.Position!=stream.Length)throw new InvalidDataException("Realtime mailbox registry has trailing data.");
            Add(identity,Path.GetFileNameWithoutExtension(path));
        }
        // The source WAL already freezes exact required recipients before any
        // Ensure/Publish. Its pending entry survives until every recipient has
        // durable handler proof and physical settlement. Recovery must run
        // before workers/ready and must never recompute current routing.
        foreach(var source in pending)
        {
            if(source.ArchiveOwnershipOnly)throw new InvalidDataException("Archive-only capture cannot authorize a live mailbox registry.");
            foreach(var target in source.Destinations)Add(target.ThreadId);
        }
        return identities.Values.ToArray();
    }
}
