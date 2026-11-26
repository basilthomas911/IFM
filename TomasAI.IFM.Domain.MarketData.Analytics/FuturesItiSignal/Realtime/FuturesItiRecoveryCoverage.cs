using System.Security.Cryptography;
namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime;

/// <summary>Exact original replay receipts. A provider replay cannot be re-created from the current epoch or wall clock.</summary>
public sealed class FuturesItiRecoveryCoverage:IDisposable
{
    const long MaximumBytes=16*1024*1024;
    FileStream file;
    readonly string path;
    readonly Dictionary<Guid,byte[]> receipts=[];
    readonly Lock gate=new();
    Exception? failure;
    void RequireWritable(){if(failure is{} error)throw new IOException("ITI receipt writer has an uncertain outcome; reopen authentic recovery evidence.",error);}
    public FuturesItiRecoveryCoverage(string directory,Guid journalRoot)
    {
        Directory.CreateDirectory(directory);
        path=Path.Combine(directory,$"{journalRoot:N}.replay-receipts");
        file=new(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.Read);
        if(file.Length>MaximumBytes||file.Length%80!=0){file.Dispose();throw new InvalidDataException("ITI replay receipt journal has a gap or exceeds its bound.");}
        Span<byte> row=stackalloc byte[80];
        while(file.Position<file.Length)
        {
            file.ReadExactly(row);
            if(!SHA256.HashData(row[..48]).AsSpan().SequenceEqual(row[48..])){file.Dispose();throw new InvalidDataException("ITI replay receipt checksum differs.");}
            var id=new Guid(row[..16]);var hash=row[16..48].ToArray();
            if(id==Guid.Empty||receipts.TryGetValue(id,out var prior)&&!prior.AsSpan().SequenceEqual(hash)){file.Dispose();throw new InvalidDataException("ITI replay receipt identity differs.");}
            receipts[id]=hash;
        }
    }
    public int Count{get{lock(gate)return receipts.Count;}}
    public bool Contains(Guid sourceId,ReadOnlySpan<byte> payload)
    {
        lock(gate)
        {
            RequireWritable();
            if(!receipts.TryGetValue(sourceId,out var prior))return false;
            if(!prior.AsSpan().SequenceEqual(SHA256.HashData(payload)))throw new InvalidDataException("Original replay source identity changed bytes.");
            return true;
        }
    }
    public void Record(Guid sourceId,ReadOnlySpan<byte> payload)
    {
        lock(gate)
        {
            if(Contains(sourceId,payload))return;
            if(sourceId==Guid.Empty||file.Length+80>MaximumBytes)throw new IOException("ITI exact replay receipt capacity exhausted; original source must remain retained.");
            Span<byte> row=stackalloc byte[80];sourceId.TryWriteBytes(row[..16]);var hash=SHA256.HashData(payload);hash.CopyTo(row[16..48]);SHA256.HashData(row[..48],row[48..]);
            try{file.Write(row);file.Flush(true);receipts.Add(sourceId,hash);}
            catch(Exception error){failure=error;throw;}
        }
    }
    public void RecordBatch(IReadOnlyList<(Guid SourceId,byte[] Payload)> sources)
    {
        lock(gate)
        {
            RequireWritable();
            var additions=new Dictionary<Guid,byte[]>();
            foreach(var(sourceId,payload)in sources)
            {
                if(sourceId==Guid.Empty)throw new InvalidDataException("ITI release source identity is absent.");
                var hash=SHA256.HashData(payload);
                if(receipts.TryGetValue(sourceId,out var prior)||additions.TryGetValue(sourceId,out prior))
                {if(!prior.AsSpan().SequenceEqual(hash))throw new InvalidDataException("ITI release source identity changed bytes.");continue;}
                additions.Add(sourceId,hash);
            }
            if(file.Length+additions.Count*80L>MaximumBytes)throw new IOException("ITI exact replay receipt capacity exhausted; original source must remain retained.");
            Span<byte> row=stackalloc byte[80];
            try
            {
                foreach(var(sourceId,hash)in additions)
                {sourceId.TryWriteBytes(row[..16]);hash.CopyTo(row[16..48]);SHA256.HashData(row[..48],row[48..]);file.Write(row);}
                if(additions.Count>0)file.Flush(true);
            }
            catch(Exception error){failure=error;throw;}
            foreach(var(sourceId,hash)in additions)receipts.Add(sourceId,hash);
        }
    }
    /// <summary>Only an acknowledged WAL prefix may retire release proofs. The replacement is atomic.</summary>
    public void Retain(IReadOnlySet<Guid> activeInputs)
    {
        lock(gate)
        {
            RequireWritable();
            if(receipts.Keys.All(activeInputs.Contains))return;
            var retained=receipts.Where(row=>activeInputs.Contains(row.Key)).ToArray();var temporary=path+".tmp";
            using(var replacement=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None))
            {
                Span<byte> row=stackalloc byte[80];
                foreach(var(sourceId,hash)in retained)
                {sourceId.TryWriteBytes(row[..16]);hash.CopyTo(row[16..48]);SHA256.HashData(row[..48],row[48..]);replacement.Write(row);}
                replacement.Flush(true);
            }
            file.Dispose();
            try{File.Move(temporary,path,true);}
            finally{file=new(path,FileMode.Open,FileAccess.ReadWrite,FileShare.Read);file.Position=file.Length;}
            receipts.Clear();foreach(var(sourceId,hash)in retained)receipts.Add(sourceId,hash);
        }
    }
    public void Dispose()=>file.Dispose();
}
