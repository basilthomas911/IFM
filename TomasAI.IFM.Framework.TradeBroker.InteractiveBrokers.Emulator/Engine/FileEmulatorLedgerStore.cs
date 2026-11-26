using System.Text.Json;
namespace TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;

/// <summary>Single-writer, streamed JSON checkpoints with flushed contents and atomic durable replacement; reads legacy heads.</summary>
public sealed class FileEmulatorLedgerStore : IEmulatorLedgerStore, IEmulatorLedgerFileEvidence, IDisposable
{
    const long MaximumBytes=256L*1024*1024;
    static readonly JsonSerializerOptions Options=new(JsonSerializerDefaults.General);
    readonly string path;
    readonly FileStream owner;
    readonly Lock gate=new();
    public FileEmulatorLedgerStore(string path)
    {
        this.path=Path.GetFullPath(path);DurableEmulatorFile.EnsureDirectory(Path.GetDirectoryName(this.path)!);
        owner=new(this.path+".owner",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
    }
    public string EffectivePath=>path;
    public EmulatorLedgerCheckpoint? Load()
    {
        lock(gate)
        {
            FileStream stream;
            try{stream=new(path,FileMode.Open,FileAccess.Read,FileShare.Read);}
            catch(FileNotFoundException){return null;}
            using(stream)
            {
                if(stream.Length is <=0 or >MaximumBytes)throw new InvalidDataException("Invalid emulator checkpoint size.");
                return JsonSerializer.Deserialize<EmulatorLedgerCheckpoint>(stream,Options)??throw new InvalidDataException("Null emulator checkpoint.");
            }
        }
    }
    public void Save(EmulatorLedgerCheckpoint checkpoint)
    {
        lock(gate)
        {
            var temporary=path+".new";
            using(var file=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None))
            {
                using var bounded=new BoundedWriteStream(file,MaximumBytes);
                JsonSerializer.Serialize(bounded,checkpoint,Options);file.Flush(true);
            }
            DurableEmulatorFile.Replace(temporary,path);
        }
    }
    public EmulatorOwnerCheckpointRequest FreezeCheckpointRequest(EmulatorOwnerCheckpointRequest proposed,bool exact)
    {
        lock(gate)
        {
            var directory=path+".checkpoints";var target=Path.Combine(directory,proposed.BoundaryId.ToString("N")+".request");
            if(File.Exists(target))
            {
                if(new FileInfo(target).Length>1024)throw new InvalidDataException("Owner checkpoint request exceeds its bound.");
                var prior=JsonSerializer.Deserialize<EmulatorOwnerCheckpointRequest>(File.ReadAllBytes(target),Options)??throw new InvalidDataException("Owner checkpoint request missing.");
                if(prior.BoundaryId!=proposed.BoundaryId||prior.RequestedAtUtc.Kind!=DateTimeKind.Utc||exact&&prior.RequestedAtUtc!=proposed.RequestedAtUtc)throw new InvalidDataException("Owner checkpoint request conflicts.");
                return prior;
            }
            DurableEmulatorFile.EnsureDirectory(directory);
            if(Directory.EnumerateFiles(directory).Take(8192).Count()>=8192||Directory.EnumerateFiles(directory,"*.request").Take(4096).Count()>=4096)throw new InvalidOperationException("Owner checkpoint request capacity reached.");
            var temporary=target+".new";
            using(var file=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None)){JsonSerializer.Serialize(file,proposed,Options);file.Flush(true);}
            DurableEmulatorFile.Replace(temporary,target);return proposed;
        }
    }
    public EmulatorLedgerFileEvidence CaptureEvidence(EmulatorLedgerCheckpoint expected)
    {
        lock(gate)
        {
            using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            if(stream.Length is <=0 or >MaximumBytes)throw new InvalidDataException("Invalid emulator checkpoint size.");
            var actual=JsonSerializer.Deserialize<EmulatorLedgerCheckpoint>(stream,Options)??throw new InvalidDataException("Null emulator checkpoint.");
            if(!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(System.Security.Cryptography.SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(expected,Options)),System.Security.Cryptography.SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(actual,Options))))
                throw new InvalidDataException("Effective emulator file does not match the committed owner.");
            stream.Position=0;return new(path,stream.Length,Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)));
        }
    }
    public void Dispose()=>owner.Dispose();
    sealed class BoundedWriteStream(Stream inner,long maximum):Stream
    {
        public override bool CanRead=>false;public override bool CanSeek=>false;public override bool CanWrite=>true;
        public override long Length=>inner.Length;public override long Position{get=>inner.Position;set=>throw new NotSupportedException();}
        public override void Flush()=>inner.Flush();
        public override void Write(byte[] buffer,int offset,int count)=>Write(buffer.AsSpan(offset,count));
        public override void Write(ReadOnlySpan<byte> buffer)
        {if(inner.Position+buffer.Length>maximum)throw new InvalidDataException("Emulator checkpoint capacity exceeded; retain input.");inner.Write(buffer);}
        public override int Read(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
        public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();
        public override void SetLength(long value)=>throw new NotSupportedException();
    }
}
