using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.MarketData.Recovery;

public sealed partial class DatabentoPositionClosingQuoteSource
{
    sealed record FixingRecord(decimal Price,long Published,long Received,uint Sequence,DateOnly Date,byte[] Original);
    readonly ConcurrentDictionary<(string Dataset,uint Publisher,uint Instrument,DateTime Expiry),Lazy<Task<FixingRecord?>>> expiryFixings=new();

    async Task<AcquiredPositionClosingQuote?> AcquireExpiryFixingAsync(PositionClosingInstrument instrument,DateOnly date,
        DateTime boundary,string key,CancellationToken token)
    {
        var convention=instrument.ExpiryConvention!;
        if(convention.Root!="ES" || convention.Dataset!="GLBX.MDP3" || convention.InstrumentId!=instrument.InstrumentId
            || instrument.UnderlyingInstrumentId==0 || instrument.UnderlyingPublisherId==0 || instrument.ReferencePayloadSha256.Length!=64)
            throw new InvalidDataException("EOD.EXPIRY.REFERENCE_UNAVAILABLE");
        var cacheKey=(instrument.Dataset,instrument.UnderlyingPublisherId,instrument.UnderlyingInstrumentId,convention.ExpirationUtc.UtcDateTime);
        if(expiryFixings.Count>=128 && !expiryFixings.ContainsKey(cacheKey))throw new InvalidOperationException("EOD.EXPIRY.CACHE_CAPACITY");
        var pending=expiryFixings.GetOrAdd(cacheKey,_=>new(()=>ReadExpiryFixingAsync(instrument,key,token),LazyThreadSafetyMode.ExecutionAndPublication));
        FixingRecord? record;
        try {record=await pending.Value.WaitAsync(token).ConfigureAwait(false);}
        catch {expiryFixings.TryRemove(new(cacheKey,pending));throw;}
        if(record is null){expiryFixings.TryRemove(new(cacheKey,pending));return null;}
        var proof=new VerifiedExpiryFixing {Convention=convention,ReferencePayloadSha256=instrument.ReferencePayloadSha256,
            UnderlyingInstrumentId=instrument.UnderlyingInstrumentId,UnderlyingPublisherId=instrument.UnderlyingPublisherId,
            FixingPrice=record.Price,ReferenceDate=record.Date,PublishedNanoseconds=record.Published,
            ReceivedNanoseconds=record.Received,Sequence=record.Sequence,CapturedAtUtc=DateTime.UtcNow};
        var quote=new StoredPositionClosingQuote {ContractId=instrument.ContractId,InstrumentId=instrument.InstrumentId,ValueDate=date,
            HasBid=false,EventTimestampNanoseconds=record.Published,ReceiveTimestampNanoseconds=record.Received,
            SourceSequence=record.Sequence,ExpiryFixing=proof,SourceArchiveSha256=Convert.ToHexString(SHA256.HashData(record.Original))};
        if(!proof.Covers(quote,boundary))throw new InvalidDataException("EOD.EXPIRY.REFERENCE_OR_FIXING_INVALID");
        VerifiedExpiryFixing.Verify(quote,record.Original);
        quote=quote with{EvidenceId=Convert.ToHexString(SHA256.HashData(MessagePackBinarySerializer.Shared.Serialize(quote)))};
        return new(quote,record.Original);
    }

    async Task<FixingRecord?> ReadExpiryFixingAsync(PositionClosingInstrument instrument,string key,CancellationToken token)
    {
        var expiry=instrument.ExpiryConvention!.ExpirationUtc.UtcDateTime;
        using var availability=new HttpRequestMessage(HttpMethod.Get,new Uri(Endpoint,"metadata.get_dataset_range?dataset="+Uri.EscapeDataString(instrument.Dataset)));
        availability.Headers.Authorization=new("Basic",Convert.ToBase64String(Encoding.UTF8.GetBytes(key+":")));
        using var available=await http.SendAsync(availability,token).ConfigureAwait(false);
        await DemandSuccessAsync(available,"metadata.get_dataset_range",token).ConfigureAwait(false);
        using var json=JsonDocument.Parse(await available.Content.ReadAsStringAsync(token).ConfigureAwait(false));
        var availableText=json.RootElement.GetProperty("schema").GetProperty("statistics").GetProperty("end").GetString()!;
        var endNs=Math.Min(TimestampNanoseconds(availableText),(expiry.AddMinutes(10).Ticks-DateTime.UnixEpoch.Ticks)*100);
        if(endNs<=(expiry.Ticks-DateTime.UnixEpoch.Ticks)*100)return null;
        var end=endNs==TimestampNanoseconds(availableText)?availableText:expiry.AddMinutes(10).ToString("O",CultureInfo.InvariantCulture);
        var parameters=new Dictionary<string,string>{["dataset"]=instrument.Dataset,["schema"]="statistics",
            ["symbols"]=instrument.UnderlyingInstrumentId.ToString(CultureInfo.InvariantCulture),["stype_in"]="instrument_id",
            ["start"]=expiry.AddMinutes(-1).ToString("O",CultureInfo.InvariantCulture),["end"]=end};
        using(var costRequest=Create("metadata.get_cost",parameters,key))
        using(var costResponse=await http.SendAsync(costRequest,token).ConfigureAwait(false))
        {
            await DemandSuccessAsync(costResponse,"metadata.get_cost",token).ConfigureAwait(false);
            var text=await costResponse.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            if(text.Length>128 || !decimal.TryParse(text.Trim().Trim('"'),NumberStyles.Float,CultureInfo.InvariantCulture,out var cost)
                || cost<0 || cost>options.MaximumRequestCostUsd)throw new InvalidOperationException("EOD.RECOVERY.HISTORICAL_COST_NOT_AUTHORIZED");
        }
        parameters["encoding"]="csv";parameters["compression"]="none";parameters["pretty_px"]="false";
        parameters["pretty_ts"]="false";parameters["map_symbols"]="false";
        using var request=Create("timeseries.get_range",parameters,key);
        using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token).ConfigureAwait(false);
        await DemandSuccessAsync(response,"timeseries.get_range",token).ConfigureAwait(false);
        await using var body=await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        using var reader=new StreamReader(body,Encoding.UTF8);
        var header=await reader.ReadLineAsync(token).ConfigureAwait(false)??throw new InvalidDataException("EOD.EXPIRY.HEADER");
        var names=header.Split(',').Select((name,index)=>(name:name.Trim('"','\uFEFF'),index)).ToDictionary(x=>x.name,x=>x.index,StringComparer.Ordinal);
        foreach(var field in new[]{"rtype","stat_type","update_action","stat_flags","publisher_id","instrument_id","ts_ref","ts_event","ts_recv","sequence","price"})
            if(!names.ContainsKey(field))throw new InvalidDataException("EOD.EXPIRY.HEADER");
        long size=Encoding.UTF8.GetByteCount(header)+1;FixingRecord? result=null;
        var day=DateOnly.FromDateTime(expiry);var dayNs=(day.ToDateTime(TimeOnly.MinValue,DateTimeKind.Utc).Ticks-DateTime.UnixEpoch.Ticks)*100;
        var startNs=(expiry.AddMinutes(-1).Ticks-DateTime.UnixEpoch.Ticks)*100;long previousReceive=0;
        while(await reader.ReadLineAsync(token).ConfigureAwait(false) is {} line)
        {
            size+=Encoding.UTF8.GetByteCount(line)+1;
            if(line.Length>16384 || size>Math.Min(options.MaximumResponseBytes,4*1024*1024))throw new InvalidDataException("EOD.EXPIRY.RESPONSE_TOO_LARGE");
            var fields=line.Split(',');if(fields.Length!=names.Count)throw new InvalidDataException("EOD.EXPIRY.ROW");
            long N(string name)=>long.Parse(fields[names[name]].Trim('"'),NumberStyles.Integer,CultureInfo.InvariantCulture);
            var received=N("ts_recv");var published=N("ts_event");
            if(N("rtype")!=24 || N("publisher_id")!=instrument.UnderlyingPublisherId || N("instrument_id")!=instrument.UnderlyingInstrumentId
                || received<startNs || received>=endNs || received<previousReceive || published<0 || published>received)
                throw new InvalidDataException("EOD.EXPIRY.WRONG_SOURCE");
            previousReceive=received;
            if(N("stat_type")!=10)continue; // A preliminary settlement price is never an expiry fixing.
            if(N("ts_ref")!=dayNs || published<(expiry.Ticks-DateTime.UnixEpoch.Ticks)*100
                || published>(expiry.AddMinutes(5).Ticks-DateTime.UnixEpoch.Ticks)*100)continue;
            if(N("update_action")==2){result=null;continue;}
            var price=N("price");var sequence=N("sequence");
            if(N("update_action")!=1 || N("stat_flags")!=0 || price<=0 || price==long.MaxValue || sequence<=0)
                throw new InvalidDataException("EOD.EXPIRY.INVALID_FIXING");
            result=new(price/1_000_000_000m,published,received,checked((uint)sequence),day,Encoding.UTF8.GetBytes(header+"\n"+line+"\n"));
        }
        return result;
    }
}
