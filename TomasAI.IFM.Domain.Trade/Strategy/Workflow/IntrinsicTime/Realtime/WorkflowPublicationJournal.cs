using System.Security.Cryptography;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Persistence;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime;

public enum WorkflowPublicationDisposition:byte { Pending,Accepted,Interrupted,Expired,Superseded }
public sealed record WorkflowPublication(Guid Key,string IntentHash,IntrinsicTimeStrategyWorkflowView Intent,Guid Session,
    ICommand<IntrinsicTimeStrategyWorkflowEntityId> Command,WorkflowPublicationDisposition Disposition);

/// <summary>Shared immutable workflow output ownership; legacy journals remain independently readable.</summary>
public sealed class WorkflowPublicationJournal:IDisposable
{
    const int MaximumBytes=8*1024*1024,PendingLimit=4096,CacheLimit=128;
    public const long DefaultMaximumRetainedBytes=8L*1024*1024*1024;
    readonly Lock gate=new();readonly string? root;readonly FileStream? lease;
    readonly long maximumRetainedBytes;long retainedBytes;
    readonly Dictionary<Guid,int> completionReservations=[];
    long CompletionReservationBytes=>completionReservations.Values.Sum(size=>(long)size);
    public long RetainedBytes{get{lock(gate)return retainedBytes;}}
    public long ReservedCompletionBytes{get{lock(gate)return root is null?0:CompletionReservationBytes;}}
    public long MaximumRetainedBytes=>maximumRetainedBytes;
    readonly Dictionary<Guid,byte[]> memory=[];readonly Dictionary<Guid,ICommand<IntrinsicTimeStrategyWorkflowEntityId>> live=[];
    readonly HashSet<Guid> pending=[];readonly Dictionary<Guid,byte[]> inputCache=[];
    int inputCount;
    public int PendingCount{get{lock(gate)return pending.Count;}}
    public int FrozenInputCount{get{lock(gate)return inputCount;}}
    public const int MaximumFrozenInputs=131072;
    public WorkflowPublicationJournal(string? directory=null):this(directory,DefaultMaximumRetainedBytes){}
    public WorkflowPublicationJournal(string? directory,long maximumRetainedBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumRetainedBytes);
        this.maximumRetainedBytes=maximumRetainedBytes;
        if(directory is null)return;
        root=Path.GetFullPath(directory);AtomicDurableFile.EnsureDirectory(root);
        lease=new(Path.Combine(root,"owner.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        try
        {
            retainedBytes=Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories).Sum(path=>new FileInfo(path).Length);
            if(retainedBytes>maximumRetainedBytes)throw new IOException("Workflow retained journal exceeds configured byte capacity; original proofs require an authoritative replay horizon.");
            AtomicDurableFile.EnsureDirectory(Path.Combine(root,"pending"));AtomicDurableFile.EnsureDirectory(Path.Combine(root,"done"));AtomicDurableFile.EnsureDirectory(Path.Combine(root,"inputs"));
            inputCount=Directory.EnumerateFiles(Path.Combine(root,"inputs"),"*.wip").Count();
            if(inputCount>MaximumFrozenInputs)throw new IOException("Workflow request recovery storage requires maintenance.");
            foreach(var file in Directory.EnumerateFiles(Path.Combine(root,"pending"),"*.wop"))
            {
                var stored=Read(file);var item=Decode(stored);if(item.Key!=Guid.ParseExact(Path.GetFileNameWithoutExtension(file),"N"))throw new InvalidDataException("Workflow output filename mismatch.");
                if(item.Disposition!=WorkflowPublicationDisposition.Pending)
                {using var mutation=RealtimeActorGeneration.EnterMutation();MoveRetained(file,PathFor(item.Key,true));continue;}
                if(pending.Count>=PendingLimit)throw new IOException("Workflow output recovery capacity exhausted.");pending.Add(item.Key);completionReservations[item.Key]=stored.Length;
            }
            if(retainedBytes+CompletionReservationBytes>maximumRetainedBytes)
                throw new IOException("Workflow retained journal lacks reserved completion capacity; original pending proofs were not removed.");
        }
        catch{lease.Dispose();throw;}
    }
    string PathFor(Guid key,bool done=false)=>Path.Combine(root!,done?"done":"pending",key.ToString("N")+".wop");
    public WorkflowPublication? Find(Guid key)
    {
        lock(gate)
        {
            byte[]? bytes=memory.GetValueOrDefault(key);
            if(bytes is null&&root is not null){try{bytes=Read(PathFor(key));}catch(FileNotFoundException){try{bytes=Read(PathFor(key,true));}catch(FileNotFoundException){return null;}}}
            if(bytes is null)return null;var item=Decode(bytes);
            if(item.Key!=key)throw new InvalidDataException("Workflow output identity mismatch.");
            if(live.TryGetValue(key,out var original))
            {
                if(!Serialize(original).AsSpan().SequenceEqual(Serialize(item.Command)))throw new InvalidDataException("Original trusted workflow output changed after capture.");
                item=item with{Command=original};
            }
            return item;
        }
    }
    public WorkflowPublication Prepare(Guid key,string digest,IntrinsicTimeStrategyWorkflowView intent,Guid session,ICommand<IntrinsicTimeStrategyWorkflowEntityId> command)
    {
        lock(gate)
        {
            if(Find(key)is{} saved){if(saved.IntentHash!=digest)throw new InvalidDataException("Accepted workflow intent changed.");return saved;}
            if(pending.Count>=PendingLimit)throw new IOException("Workflow output capacity exhausted; retain source.");
            var item=new WorkflowPublication(key,digest,intent,session,command,WorkflowPublicationDisposition.Pending);
            pending.Add(key);
            try{Write(item);}
            catch
            {
                if(root is null){pending.Remove(key);completionReservations.Remove(key);}
                else try{_=File.GetAttributes(PathFor(key));}catch(FileNotFoundException){pending.Remove(key);completionReservations.Remove(key);}catch(DirectoryNotFoundException){pending.Remove(key);completionReservations.Remove(key);}
                throw;
            }
            // Write has already frozen and durably saved this exact immutable
            // item. Keep the original trusted command; decoding its entire input
            // immediately would create a second large graph without new evidence.
            live[key]=command;return item;
        }
    }
    public WorkflowPublication[] Pending(){lock(gate)return pending.Select(key=>Find(key)??throw new InvalidDataException("Missing workflow output.")).OrderBy(x=>x.Intent.UpdatedAtUtc).ThenBy(x=>x.Intent.WorkflowRevision).ToArray();}
    public void Confirm(Guid key,WorkflowPublicationDisposition disposition)
    {
        if(disposition==WorkflowPublicationDisposition.Pending)throw new ArgumentException("Owner disposition required.",nameof(disposition));
        lock(gate)
        {
            var item=Find(key)??throw new InvalidDataException("Unknown workflow output.");
            if(item.Disposition==WorkflowPublicationDisposition.Pending)WriteDisposition(key,disposition);
            if(root is not null)
            {
                try{_=File.GetAttributes(PathFor(key));}
                catch(FileNotFoundException){pending.Remove(key);completionReservations.Remove(key);live.Remove(key);return;}
                using var mutation=RealtimeActorGeneration.EnterMutation();MoveRetained(PathFor(key),PathFor(key,true));
            }
            pending.Remove(key);completionReservations.Remove(key);live.Remove(key);
            if(root is not null&&memory.Count>CacheLimit)foreach(var old in memory.Keys.Where(x=>!pending.Contains(x)).Take(memory.Count-CacheLimit).ToArray())memory.Remove(old);
        }
    }
    public T FreezeInput<T>(Guid key,string digest,Func<T> create)where T:class
    {
        lock(gate)
        {
            var path=root is null?null:Path.Combine(root,"inputs",key.ToString("N")+".wip");
            byte[]? bytes=inputCache.GetValueOrDefault(key);
            if(bytes is null&&path is not null)try{bytes=Read(path);}catch(FileNotFoundException){}
            if(bytes is not null)
            {
                Verify(bytes);using var reader=new BinaryReader(new MemoryStream(bytes,32,bytes.Length-32,false));
                var version=reader.ReadInt32();
                if(version is not(1 or 2)||reader.ReadString()!=typeof(T).FullName||reader.ReadString()!=digest)throw new InvalidDataException("Frozen workflow request identity changed.");
                var size=reader.ReadInt32();if(size<=0||size>MaximumBytes||size>reader.BaseStream.Length-reader.BaseStream.Position)throw new InvalidDataException("Invalid frozen workflow input.");
                var source=bytes.AsMemory(32+checked((int)reader.BaseStream.Position),size);
                reader.BaseStream.Position+=size;
                var restored=MessagePackSerializer.Deserialize<T>(source);
                if(version==2)MessageOrigins.Restore(restored,reader.ReadBoolean()?MessageOriginBinary.Deserialize(reader.ReadString()):null);
                if(reader.BaseStream.Position!=reader.BaseStream.Length)throw new InvalidDataException("Trailing frozen workflow input data.");
                return restored;
            }
            if(inputCount>=MaximumFrozenInputs)throw new IOException("Workflow request recovery capacity exhausted; retain source.");
            var value=create();var data=MessagePackSerializer.Serialize(value);
            var origin=MessageOrigins.Get(value);var originBytes=origin is null?null:MessageOriginBinary.Serialize(origin);
            using var payload=new MemoryStream(PayloadCapacity(data.Length,typeof(T).FullName!,digest,originBytes??""));
            using(var writer=new BinaryWriter(payload,System.Text.Encoding.UTF8,true))
            {writer.Write(2);writer.Write(typeof(T).FullName!);writer.Write(digest);writer.Write(data.Length);writer.Write(data);
                writer.Write(originBytes is not null);if(originBytes is not null)writer.Write(originBytes);}
            var frame=Frame(payload);if(path is not null)Save(path,frame);inputCount++;inputCache[key]=frame;
            if(root is not null&&inputCache.Count>CacheLimit)inputCache.Remove(inputCache.Keys.First());
            return MessageOrigins.Copy(value,MessagePackSerializer.Deserialize<T>(data));
        }
    }
    static byte[] Serialize(ICommand<IntrinsicTimeStrategyWorkflowEntityId> command)=>MessagePackSerializer.Serialize(command.GetType(),command);
    void Write(WorkflowPublication item)
    {
        if(item.Key==Guid.Empty||item.IntentHash.Length!=64||item.Command.CommandId==Guid.Empty||item.Intent.EntityId!=item.Command.EntityId)throw new InvalidDataException("Malformed workflow publication.");
        var view=MessagePackSerializer.Serialize(item.Intent);var data=Serialize(item.Command);var kind=item.Command.GetType().Name;
        var origin=MessageOrigins.Get(item.Command);var originBytes=origin is null?null:MessageOriginBinary.Serialize(origin);
        using var payload=new MemoryStream(PayloadCapacity(checked(view.Length+data.Length),item.IntentHash,kind,originBytes??""));
        using(var writer=new BinaryWriter(payload,System.Text.Encoding.UTF8,true))
        {
            writer.Write(1);writer.Write(item.Key.ToByteArray());writer.Write(item.IntentHash);writer.Write(item.Session.ToByteArray());writer.Write((byte)item.Disposition);
            writer.Write(view.Length);writer.Write(view);
            writer.Write(kind);writer.Write(data.Length);writer.Write(data);
            writer.Write(originBytes is not null);if(originBytes is not null)writer.Write(originBytes);
        }
        var frame=Frame(payload);
        if(root is not null)
        {
            if(item.Disposition==WorkflowPublicationDisposition.Pending)completionReservations[item.Key]=frame.Length;
            Save(PathFor(item.Key),frame,item.Disposition==WorkflowPublicationDisposition.Pending?null:item.Key);
        }
        memory[item.Key]=frame;
    }
    void WriteDisposition(Guid key,WorkflowPublicationDisposition disposition)
    {
        // Find has already validated the complete frame and original trusted
        // command. Keep every accepted source/command/provenance byte exact;
        // only the existing status byte and its existing checksum change.
        var original=memory.GetValueOrDefault(key)??Read(PathFor(key));Verify(original);
        using var reader=new BinaryReader(new MemoryStream(original,32,original.Length-32,false));
        if(reader.ReadInt32()!=1||new Guid(reader.ReadBytes(16))!=key)throw new InvalidDataException("Workflow confirmation identity mismatch.");
        _=reader.ReadString();_=reader.ReadBytes(16);
        var offset=32+checked((int)reader.BaseStream.Position);
        if(reader.ReadByte()!=(byte)WorkflowPublicationDisposition.Pending)throw new InvalidDataException("Workflow confirmation source is not pending.");
        var updated=original.ToArray();updated[offset]=(byte)disposition;
        SHA256.HashData(updated.AsSpan(32),updated.AsSpan(0,32));
        if(root is not null)Save(PathFor(key),updated,key);
        memory[key]=updated;
    }
    static int PayloadCapacity(int binaryLength,params string[] text)
    {
        var capacity=checked(binaryLength+256);
        foreach(var value in text)capacity=checked(capacity+System.Text.Encoding.UTF8.GetByteCount(value));
        return Math.Min(capacity,MaximumBytes-32);
    }
    static byte[] Frame(MemoryStream payload)
    {
        if(payload.Length>MaximumBytes-32)throw new InvalidDataException("Workflow output frame too large.");
        var data=payload.GetBuffer().AsSpan(0,checked((int)payload.Length));
        var bytes=new byte[data.Length+32];SHA256.HashData(data,bytes.AsSpan(0,32));data.CopyTo(bytes.AsSpan(32));return bytes;
    }
    void Save(string path,byte[] bytes,Guid? completing=null)
    {
        var temporary=path+".tmp";var prior=FileLength(path)+FileLength(temporary);
        var remainingReservations=CompletionReservationBytes-(completing is{} key?completionReservations.GetValueOrDefault(key):0);
        // A pending owner retains exact capacity for its byte-identical frame
        // confirmation rewrite. Refusal happens before touching new source data.
        if(checked(retainedBytes-FileLength(temporary)+bytes.Length+remainingReservations)>maximumRetainedBytes)
            throw new IOException("Workflow retained journal byte capacity exhausted; new source was not accepted and no original proof was evicted.");
        using var mutation=RealtimeActorGeneration.EnterMutation();
        try
        {
            using(var file=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None)){file.Write(bytes);file.Flush(true);}
            AtomicDurableFile.Replace(temporary,path);
        }
        finally { retainedBytes=checked(retainedBytes-prior+FileLength(path)+FileLength(temporary)); }
    }
    void MoveRetained(string source,string destination)
    {
        var prior=FileLength(source)+FileLength(destination);
        try { AtomicDurableFile.Replace(source,destination); }
        finally { retainedBytes=checked(retainedBytes-prior+FileLength(source)+FileLength(destination)); }
    }
    static long FileLength(string path)
    {
        try{return new FileInfo(path).Length;}
        catch(FileNotFoundException){return 0;}
        catch(DirectoryNotFoundException){return 0;}
    }
    static byte[] Read(string path){using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);if(file.Length is <33 or >MaximumBytes)throw new InvalidDataException("Invalid workflow frame size.");var bytes=new byte[(int)file.Length];file.ReadExactly(bytes);Verify(bytes);return bytes;}
    static void Verify(byte[] bytes){if(bytes.Length is <33 or >MaximumBytes||!CryptographicOperations.FixedTimeEquals(SHA256.HashData(bytes.AsSpan(32)),bytes.AsSpan(0,32)))throw new InvalidDataException("Workflow publication checksum mismatch.");}
    static WorkflowPublication Decode(byte[] bytes)
    {
        Verify(bytes);using var reader=new BinaryReader(new MemoryStream(bytes,32,bytes.Length-32,false));
        if(reader.ReadInt32()!=1)throw new InvalidDataException("Unsupported workflow output format.");
        var key=new Guid(reader.ReadBytes(16));var digest=reader.ReadString();var session=new Guid(reader.ReadBytes(16));var status=(WorkflowPublicationDisposition)reader.ReadByte();
        ReadOnlyMemory<byte> Blob()
        {
            var count=reader.ReadInt32();if(count<=0||count>MaximumBytes||count>reader.BaseStream.Length-reader.BaseStream.Position)throw new InvalidDataException("Invalid workflow output payload.");
            var result=bytes.AsMemory(32+checked((int)reader.BaseStream.Position),count);reader.BaseStream.Position+=count;return result;
        }
        var view=MessagePackSerializer.Deserialize<IntrinsicTimeStrategyWorkflowView>(Blob());var kind=reader.ReadString();var type=Types.SingleOrDefault(t=>t.Name==kind)??throw new InvalidDataException("Unknown workflow output command.");
        var command=(ICommand<IntrinsicTimeStrategyWorkflowEntityId>)MessagePackSerializer.Deserialize(type,Blob());
        MessageOrigins.Restore(command,reader.ReadBoolean()?MessageOriginBinary.Deserialize(reader.ReadString()):null);
        if(reader.BaseStream.Position!=reader.BaseStream.Length||!Enum.IsDefined(status)||digest.Length!=64||view.EntityId!=command.EntityId)throw new InvalidDataException("Malformed workflow publication.");
        return new(key,digest,view,session,command,status);
    }
    static readonly Type[] Types=[typeof(CompleteRegimeDiscoveryCommand),typeof(FailRegimeDiscoveryCommand),typeof(CompleteMarketConditionCommand),typeof(FailMarketConditionCommand),typeof(CompleteTradeSelectionCommand),typeof(FailTradeSelectionCommand),typeof(CompleteTradeSelectionReservationCommand),typeof(TimeoutTradeSelectionCommand),typeof(CompleteOrderCompositionCommand),typeof(FailOrderCompositionCommand),typeof(TimeoutOrderCompositionCommand),typeof(AcceptOrderCompositionPreparationCommand),typeof(PrepareRiskManagementCommand),typeof(CompleteRiskManagementCommand),typeof(FailRiskManagementCommand),typeof(AdvanceRiskFinancialHandoffCommand),typeof(RedispatchCurrentStrategyPipelineCommand)];
    public void Dispose()=>lease?.Dispose();
}
