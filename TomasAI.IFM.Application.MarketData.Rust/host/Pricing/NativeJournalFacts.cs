using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;
using TomasAI.IFM.Application.MarketData.Rust.Publication;
namespace TomasAI.IFM.Application.MarketData.Rust.Host;

/// <summary>Passive copied business-journal/reconstruction facts. No native
/// runtime, provider context, lease, callback, renewal or realization state is
/// retained. Rust owns all decisions about applying the committed snapshot.</summary>
public sealed class NativeJournalFacts(IDurableSubscriptionIntentStore journal,ICompositionRoutePlanStore plans,TimeProvider? retentionTime=null)
{
    const int MaximumBytes=32*1024*1024,PageBytes=32768;
    readonly TimeProvider retention=retentionTime??TimeProvider.System;
    readonly object gate=new();readonly Dictionary<string,Snapshot> snapshots=[];
    int building;
    sealed record Snapshot(byte[] Bytes,string Digest,long Created);
    /// <summary>Captures bounded immutable raw facts or copies one byte page.
    /// This separate source protocol supports the existing 16 MiB journal plus
    /// reconstruction metadata without increasing the 4 MiB raw-response bound.</summary>
    public async Task<byte[]> ReadAsync(JsonElement input,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(input.TryGetProperty("action",out _))return await DeliveryAsync(input,token).ConfigureAwait(false);
        if(input.TryGetProperty("snapshot",out var requested)) {
            var id=requested.GetString();var offset=input.GetProperty("offset").GetInt32();
            if(id is null||id.Length!=32||offset<0||offset%PageBytes!=0)throw new InvalidDataException("Invalid journal snapshot page.");
            Snapshot snapshot;lock(gate){if(!snapshots.TryGetValue(id,out snapshot!)||retention.GetElapsedTime(snapshot.Created)>=TimeSpan.FromMinutes(2)){snapshots.Remove(id);throw new InvalidDataException("Journal snapshot expired.");}}
            if(offset>=snapshot.Bytes.Length)throw new InvalidDataException("Journal page exceeds snapshot.");
            return snapshot.Bytes.AsSpan(offset,Math.Min(PageBytes,snapshot.Bytes.Length-offset)).ToArray();
        }
        var scope=input.GetProperty("authority").GetString();var dataset=input.GetProperty("dataset").GetString();
        if(string.IsNullOrWhiteSpace(scope)||scope.Length>128||dataset!="GLBX.MDP3")throw new InvalidDataException("Invalid raw journal scope.");
        lock(gate){
            foreach(var id in snapshots.Where(item=>retention.GetElapsedTime(item.Value.Created)>=TimeSpan.FromMinutes(2)).Select(item=>item.Key).ToArray())snapshots.Remove(id);
            if(building!=0)throw new InvalidOperationException("Raw journal capture is already in progress.");building++;
        }
        try {
            var snapshot=DurableSubscriptionContract.Freeze(await journal.ReadAsync(scope,dataset,token).ConfigureAwait(false));
            if(snapshot.Scope!=scope||snapshot.Dataset!=dataset)throw new InvalidDataException("Raw journal source identity changed.");
            var ids=snapshot.Authorities.SelectMany(authority=>authority.Leases).Select(lease=>lease.Ticker.PricingPlanId
                ??throw new InvalidDataException("Committed ownership lacks an immutable reconstruction plan.")).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            if(ids.Length>8)throw new InvalidDataException("Committed reconstruction-plan capacity exceeded.");
            var recipes=new List<object>();
            foreach(var id in ids) {
                var plan=await plans.ReadAsync(id,token).ConfigureAwait(false)??throw new InvalidDataException("Committed reconstruction plan is unavailable.");
                plan.Validate();if(plan.PlanId!=id||plan.Dataset!=dataset)throw new InvalidDataException("Committed reconstruction identity changed.");
                recipes.Add(new{id=plan.PlanId,schema=plan.SchemaVersion,dataset=plan.Dataset,maturity=plan.MaturityDate.DayNumber,
                    candidates=NativeReferenceProjection.Candidates(plan.Options),
                    references=plan.Options.IsEmpty?null:NativeReferenceProjection.References(plan.Calendar!,plan.Publication!,plan.Conversion!),
                    futures=plan.Futures.Select(future=>new object[]{future.ContractId,future.Root,future.Dataset,future.Exchange,future.Currency,
                        future.LastTradingUtc.UtcTicks-DateTime.UnixEpoch.Ticks,(long)future.LastTradingUtc.Offset.TotalMinutes,
                        future.Multiplier.ToString(CultureInfo.InvariantCulture),future.TickSize.ToString(CultureInfo.InvariantCulture),future.DefinitionDigest}),
                    routes=plan.NativeFutures.Select(route=>new object[]{route.DomainContractId,route.ProviderContractName,(int)route.AssetTypeId,route.RootSymbol,route.Dataset,route.OnTheRun,route.Rollover}),
                    separate=plan.SeparateContractConnection,independent=plan.IndependentChainConnection});
            }
            var bytes=Encode(new{schema=snapshot.SchemaVersion,authority=snapshot.Scope,dataset=snapshot.Dataset,revision=snapshot.Revision,digest=DurableSubscriptionContract.Digest(snapshot),
                authorities=snapshot.Authorities.Select(authority=>new{id=authority.SourceId,version=authority.SourceVersion,event_id=authority.SourceEventId.ToString("N"),digest=authority.FactDigest,status=(int)authority.Status,
                    reason=authority.ReasonCode,workflow_type=authority.Owner.WorkflowType,workflow_id=authority.Owner.WorkflowId,leg_id=authority.Owner.LegId,
                    leases=authority.Leases.Select(lease=>new object?[]{lease.LeaseId.ToString("N"),lease.LeaseVersion,(int)lease.Purpose,
                        lease.Ticker.ProviderScope,lease.Ticker.Dataset,lease.Ticker.ContractId,lease.Ticker.Schema,(int)lease.Ticker.AssetKind,lease.Ticker.UnderlyingContractId,lease.Ticker.PricingPlanId})}),plans=recipes});
            if(bytes.Length>MaximumBytes)throw new InvalidDataException("Raw journal and reconstruction source exceeds capacity.");
            var entry=new Snapshot(bytes,Convert.ToHexString(SHA256.HashData(bytes)),retention.GetTimestamp());var key=Guid.NewGuid().ToString("N");
            lock(gate){while(snapshots.Count>=2)snapshots.Remove(snapshots.MinBy(item=>item.Value.Created).Key);snapshots.Add(key,entry);}
            return JsonSerializer.SerializeToUtf8Bytes(new{id=key,total=bytes.Length,digest=entry.Digest,page_bytes=PageBytes});
        }finally{lock(gate)building--;}
    }
    // This port only copies business rows or applies explicit conditional writes.
    // Rust chooses when actual dataset realization permits acknowledgement.
    async Task<byte[]> DeliveryAsync(JsonElement input,CancellationToken token)
    {
        var scope=input.GetProperty("authority").GetString();var dataset=input.GetProperty("dataset").GetString();
        if(string.IsNullOrWhiteSpace(scope)||scope.Length>128||dataset!="GLBX.MDP3")throw new InvalidDataException("Invalid journal delivery namespace.");
        var action=input.GetProperty("action").GetString();
        if(action is not("pending" or "ack"))throw new InvalidDataException("Invalid journal delivery action.");
        var current=DurableSubscriptionContract.Freeze(await journal.ReadAsync(scope,dataset,token).ConfigureAwait(false));
        if(current.Scope!=scope||current.Dataset!=dataset)throw new InvalidDataException("Journal delivery namespace changed.");
        var digest=DurableSubscriptionContract.Digest(current);
        var revision=input.GetProperty("revision").GetInt64();var expected=input.GetProperty("digest").GetString();
        if(revision<0||expected?.Length!=64||!expected.All(Uri.IsHexDigit))throw new InvalidDataException("Invalid journal delivery watermark.");
        if(current.Revision!=revision||!string.Equals(expected,digest,StringComparison.OrdinalIgnoreCase))return JsonSerializer.SerializeToUtf8Bytes(new{accepted=false,revision=current.Revision,digest,rows=Array.Empty<object>()});
        if(action=="pending"){
            var rows=await journal.ReadPendingOutboxAsync(scope,dataset,100,token).ConfigureAwait(false);
            if(rows.Count>100||rows.Any(row=>row.TransitionId==Guid.Empty||row.Revision<0)||rows.Select(row=>row.TransitionId).Distinct().Count()!=rows.Count)throw new InvalidDataException("Invalid journal delivery rows.");
            return JsonSerializer.SerializeToUtf8Bytes(new{accepted=true,revision,digest,rows=rows.Select(row=>new object[]{row.TransitionId.ToString("N"),row.Revision})});
        }
        var requested=input.GetProperty("ids");if(requested.ValueKind!=JsonValueKind.Array||requested.GetArrayLength() is <1 or >100)throw new InvalidDataException("Invalid journal acknowledgement capacity.");
        var ids=requested.EnumerateArray().Select(row=>Guid.TryParseExact(row.GetString(),"N",out var id)&&id!=Guid.Empty?id:throw new InvalidDataException("Invalid journal acknowledgement identity.")).ToArray();
        if(ids.Distinct().Count()!=ids.Length)throw new InvalidDataException("Duplicate journal acknowledgement identity.");
        foreach(var id in ids)if(!await journal.AcknowledgeOutboxAsync(scope,dataset,id,token).ConfigureAwait(false))throw new IOException("Journal delivery acknowledgement was not committed.");
        return JsonSerializer.SerializeToUtf8Bytes(new{accepted=true,revision,digest,rows=Array.Empty<object>()});
    }
    static byte[] Encode<T>(T value)
    {
        var buffer=new LimitedBuffer();
        using(var writer=new Utf8JsonWriter(buffer)){JsonSerializer.Serialize(writer,value);writer.Flush();}
        return buffer.Copy();
    }
    // Bound before serializer growth, including its reserved output span. A
    // small span reserve permits a valid payload exactly at the byte boundary;
    // Advance enforces the actual written-byte limit independently of that hint.
    sealed class LimitedBuffer:System.Buffers.IBufferWriter<byte>
    {
        byte[] bytes=new byte[8192];int written;
        public void Advance(int count){if(count<0||count>bytes.Length-written||count>MaximumBytes-written)throw new InvalidDataException("Raw journal source exceeds byte capacity.");written+=count;}
        void Ensure(int hint){
            if(hint<0)throw new ArgumentOutOfRangeException(nameof(hint));
            var required=(long)written+Math.Max(1,hint);
            if(required>MaximumBytes+65536L)throw new InvalidDataException("Raw journal serializer reservation exceeds capacity.");
            if(required<=bytes.Length)return;
            var capacity=(int)Math.Min(MaximumBytes+65536L,Math.Max(required,2L*bytes.Length));
            Array.Resize(ref bytes,capacity);
        }
        public Memory<byte> GetMemory(int sizeHint=0){Ensure(sizeHint);return bytes.AsMemory(written);}
        public Span<byte> GetSpan(int sizeHint=0){Ensure(sizeHint);return bytes.AsSpan(written);}
        public byte[] Copy()=>bytes.AsSpan(0,written).ToArray();
    }

}
