using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.MarketData.Rust.Publication;
namespace TomasAI.IFM.Application.MarketData.Rust.Host;

/// <summary>Passive immutable definition snapshots. Only compact copied bytes
/// are retained: at most two snapshots and 64 MiB retained (plus one bounded capture), never native resources.
/// Native consumers select contracts before requesting full reviewed models.</summary>
public sealed class NativeDefinitionFacts(IDbContextFactory databases,TimeProvider? retentionTime=null)
{
    const int MaximumRows=100000,MaximumBytes=64*1024*1024,PageRows=256;
    readonly TimeProvider retention=retentionTime??TimeProvider.System;
    readonly object gate=new();
    readonly Dictionary<string,Snapshot> snapshots=[];
    int building;
    sealed record Snapshot(byte[][] Rows,string Digest,long Created);
    /// <summary>Captures a complete compact source or reads an immutable bounded
    /// page. A snapshot identity prevents mixing data from concurrent refreshes.</summary>
    public async Task<byte[]> ReadAsync(JsonElement input,CancellationToken token)
    {
        if(input.TryGetProperty("snapshot",out var identity))
        {
            var id=identity.GetString();var offset=input.GetProperty("offset").GetInt32();
            if(id is null||id.Length!=32||offset<0||offset%PageRows!=0)throw new InvalidDataException("Invalid definition snapshot page.");
            Snapshot snapshot;
            lock(gate){if(!snapshots.TryGetValue(id,out snapshot!)||retention.GetElapsedTime(snapshot.Created)>=TimeSpan.FromMinutes(2)){snapshots.Remove(id);throw new InvalidDataException("Definition snapshot expired.");}}
            if(offset>=snapshot.Rows.Length)throw new InvalidDataException("Definition page exceeds snapshot.");
            return Page(snapshot.Rows,offset);
        }
        var root=input.GetProperty("root").GetString();
        var first=DateOnly.FromDayNumber(input.GetProperty("from").GetInt32());var last=DateOnly.FromDayNumber(input.GetProperty("until").GetInt32());
        if(string.IsNullOrWhiteSpace(root)||root.Length>60||last<first||last.DayNumber-first.DayNumber>2000)throw new InvalidDataException("Invalid compact option definition range.");
        var underlying=input.TryGetProperty("underlying",out var exact)?exact.GetString():null;
        var browser=input.TryGetProperty("browser",out var browserFlag)&&browserFlag.GetBoolean();
        string[]? roots=input.TryGetProperty("provider_roots",out var providerRoots)
            ?providerRoots.EnumerateArray().Select(value=>value.GetString()??throw new InvalidDataException("Invalid raw provider root.")).ToArray():null;
        if(underlying is not null&&(string.IsNullOrWhiteSpace(underlying)||underlying.Length>1024||first!=last)
            ||browser&&underlying is null||roots is{Length:>64}||roots?.Any(value=>string.IsNullOrWhiteSpace(value)||value.Length>128)==true)
            throw new InvalidDataException("Invalid exact raw option source filter.");
        lock(gate){
            foreach(var expired in snapshots.Where(x=>retention.GetElapsedTime(x.Value.Created)>=TimeSpan.FromMinutes(2)).Select(x=>x.Key).ToArray())snapshots.Remove(expired);
            if(building!=0)throw new InvalidOperationException("Definition capture already in progress.");
            building++;
        }
        try{
            (DateOnly ExpiryDate,string ContractId)[] expiries;
            if(underlying is not null)expiries=[(first,underlying)];
            else {
                var saved=await databases.SecuritiesDb.GetOptionContractExpiriesAsync(root,first,last,token).ConfigureAwait(false);
                if(saved.Count>2048)throw new InvalidDataException("Raw option expiry capacity exceeded.");
                expiries=saved.Select(value=>(value.ExpiryDate,value.ContractId)).ToArray();
            }
            var rows=new List<byte[]>();var count=0;
            // The existing C# universe builder uses TryAdd by canonical contract
            // across the entire expiry range. Calendar partitions may overlap;
            // preserve that first-row authority before freezing native pages.
            // Rust still rejects duplicate identities in a supposedly unique wire snapshot.
            var identities=new HashSet<string>(StringComparer.Ordinal);
            using var digest=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach(var expiry in expiries.Distinct()){
                token.ThrowIfCancellationRequested();
                var cached=await databases.SecuritiesDb.GetCachedOptionContractDefinitionsAsync(root,expiry.ContractId,expiry.ExpiryDate,roots,token).ConfigureAwait(false);
                foreach(var row in cached){
                    token.ThrowIfCancellationRequested();
                    if(!identities.Add(row.Definition.ContractId))continue;
                    var bytes=JsonSerializer.SerializeToUtf8Bytes(browser?NativeReferenceProjection.BrowserOption(row.Definition):NativeReferenceProjection.CoverageOption(row.Definition));
                    count=checked(count+bytes.Length);
                    if(rows.Count==MaximumRows||count>MaximumBytes)throw new InvalidDataException("Compact definition capacity exceeded.");
                    rows.Add(bytes);
                }
            }
            var captured=rows.ToArray();
            for(var start=0;start<captured.Length;start+=PageRows)digest.AppendData(Page(captured,start));
            var value=new Snapshot(captured,Convert.ToHexString(digest.GetHashAndReset()),retention.GetTimestamp());var key=Guid.NewGuid().ToString("N");
            lock(gate){
                while(snapshots.Count>=2||snapshots.Values.Sum(x=>x.Rows.Sum(row=>row.Length))+count>MaximumBytes){
                    snapshots.Remove(snapshots.MinBy(x=>x.Value.Created).Key);
                }
                snapshots.Add(key,value);
            }
            return JsonSerializer.SerializeToUtf8Bytes(new{id=key,count=value.Rows.Length,digest=value.Digest});
        }finally{lock(gate)building--;}
    }
    static byte[] Page(byte[][] rows,int offset)
    {
        using var output=new MemoryStream();using(var writer=new Utf8JsonWriter(output)){
            writer.WriteStartArray();foreach(var row in rows.Skip(offset).Take(PageRows))writer.WriteRawValue(row);writer.WriteEndArray();
        }
        return output.ToArray();
    }

}
