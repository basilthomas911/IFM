using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Application.MarketData.Recovery;

public sealed record ClosingQuoteAcquisitionOptions
{
    // Paid historical data requires an explicit operator-configured allowance.
    public decimal MaximumRequestCostUsd { get; init; } = 0;
    public long MaximumResponseBytes { get; init; } = 64 * 1024 * 1024;
}

/// <summary>Streams exact MBP-1 records; retains only the selected original CSV record as immutable evidence.</summary>
public sealed partial class DatabentoPositionClosingQuoteSource(HttpClient http, ClosingQuoteAcquisitionOptions options,
    Func<string?>? getApiKey = null) : IPositionClosingQuoteSource, IDisposable
{
    static readonly Uri Endpoint = new("https://hist.databento.com/v0/");
    public void Dispose() => http.Dispose();

    public async Task<AcquiredPositionClosingQuote?> AcquireAsync(PositionClosingInstrument instrument, DateOnly date,
        DateTime boundaryUtc, CancellationToken token)
    {
        if (boundaryUtc.Kind!=DateTimeKind.Utc || instrument.InstrumentId==0 || options.MaximumRequestCostUsd<0 || options.MaximumResponseBytes<=0)
            throw new ArgumentException("Invalid closing quote request.");
        var key = getApiKey is null ? Environment.GetEnvironmentVariable("DATABENTO_API_KEY") : getApiKey();
        if(string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("DATABENTO_API_KEY is required for closing-quote recovery.");
        if(instrument.IsOption && instrument.ExpiryConvention is { } expiry && expiry.ExpirationUtc.UtcDateTime<=boundaryUtc)
            return await AcquireExpiryFixingAsync(instrument,date,boundaryUtc,key,token).ConfigureAwait(false);
        // The historical receive-time watermark can stop exactly at session close.
        // Keep the bounded late-receipt allowance only where the MBP-1 schema has data.
        using var availabilityRequest = new HttpRequestMessage(HttpMethod.Get,
            new Uri(Endpoint,"metadata.get_dataset_range?dataset="+Uri.EscapeDataString(instrument.Dataset)));
        availabilityRequest.Headers.Authorization=new AuthenticationHeaderValue("Basic",Convert.ToBase64String(Encoding.UTF8.GetBytes(key+":")));
        using var availabilityResponse=await http.SendAsync(availabilityRequest,token).ConfigureAwait(false);
        await DemandSuccessAsync(availabilityResponse,"metadata.get_dataset_range",token).ConfigureAwait(false);
        var availabilityText=await availabilityResponse.Content.ReadAsStringAsync(token).ConfigureAwait(false);
        if(availabilityText.Length>65536)throw new InvalidDataException("EOD.RECOVERY.INVALID_AVAILABILITY");
        using var availability=JsonDocument.Parse(availabilityText);
        if(!availability.RootElement.TryGetProperty("schema",out var schemas)
            || !schemas.TryGetProperty("mbp-1",out var schema) || !schema.TryGetProperty("end",out var endValue)
            || endValue.ValueKind!=JsonValueKind.String)
            throw new InvalidDataException("EOD.RECOVERY.INVALID_AVAILABILITY");
        var availableEnd=endValue.GetString()!;
        var availableEndNs=TimestampNanoseconds(availableEnd);
        var desiredEnd=boundaryUtc.AddSeconds(15);
        var desiredEndNs=checked((desiredEnd.Ticks-DateTime.UnixEpoch.Ticks)*100);
        var end=availableEndNs<desiredEndNs?availableEnd:desiredEnd.ToString("O",CultureInfo.InvariantCulture);
        var endNs=Math.Min(availableEndNs,desiredEndNs);
        foreach(var minutes in new[]{1,5,15})
        {
            var start=boundaryUtc.AddMinutes(-minutes);
            var startNs=checked((start.Ticks-DateTime.UnixEpoch.Ticks)*100);
            if(endNs<=startNs)continue; // Widen the eligible window, or let the durable job retry later.
            var parameters = new Dictionary<string,string> {
                ["dataset"]=instrument.Dataset,["schema"]="mbp-1",["symbols"]=instrument.RawSymbol ?? instrument.InstrumentId.ToString(CultureInfo.InvariantCulture),
                ["stype_in"]=instrument.RawSymbol is null ? "instrument_id" : "raw_symbol",["start"]=start.ToString("O",CultureInfo.InvariantCulture),
                // MBP-1 ranges filter on receive time. Include bounded late receipts, then enforce event time <= close below.
                ["end"]=end
            };
            using(var request=Create("metadata.get_cost",parameters,key))
            using(var response=await http.SendAsync(request,token).ConfigureAwait(false))
            {
                await DemandSuccessAsync(response,"metadata.get_cost",token).ConfigureAwait(false);
                var text=await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                if(text.Length>128 || !decimal.TryParse(text.Trim().Trim('"'),NumberStyles.Float,CultureInfo.InvariantCulture,out var cost)
                    || cost<0 || cost>options.MaximumRequestCostUsd)
                    throw new InvalidOperationException("EOD.RECOVERY.HISTORICAL_COST_NOT_AUTHORIZED");
            }
            parameters["encoding"]="csv";parameters["compression"]="none";parameters["pretty_px"]="false";
            parameters["pretty_ts"]="false";parameters["map_symbols"]="false";
            using var rangeRequest=Create("timeseries.get_range",parameters,key);
            using var rangeResponse=await http.SendAsync(rangeRequest,HttpCompletionOption.ResponseHeadersRead,token).ConfigureAwait(false);
            await DemandSuccessAsync(rangeResponse,"timeseries.get_range",token).ConfigureAwait(false);
            if(rangeResponse.Content.Headers.ContentLength>options.MaximumResponseBytes) throw new InvalidDataException("EOD.RECOVERY.RESPONSE_TOO_LARGE");
            await using var stream=await rangeResponse.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            using var reader=new StreamReader(stream,Encoding.UTF8,false,4096,leaveOpen:true);
            var header=await reader.ReadLineAsync(token).ConfigureAwait(false);
            if(header is null) continue;
            var columns=header.Split(',').Select((name,index)=>(name:name.Trim('"','\uFEFF'),index)).ToDictionary(x=>x.name,x=>x.index,StringComparer.Ordinal);
            foreach(var required in new[]{"instrument_id","ts_event","ts_recv","sequence","bid_px_00","ask_px_00","bid_sz_00","ask_sz_00"})
                if(!columns.ContainsKey(required))throw new InvalidDataException("EOD.RECOVERY.INVALID_MBP1_HEADER");
            long bytes=Encoding.UTF8.GetByteCount(header)+1;
            StoredPositionClosingQuote? latest=null;string? selected=null;
            while(await reader.ReadLineAsync(token).ConfigureAwait(false) is { } line)
            {
                bytes+=Encoding.UTF8.GetByteCount(line)+1;
                if(bytes>options.MaximumResponseBytes || line.Length>16384)throw new InvalidDataException("EOD.RECOVERY.RESPONSE_TOO_LARGE");
                var fields=line.Split(','); if(fields.Length!=columns.Count)throw new InvalidDataException("EOD.RECOVERY.INVALID_MBP1_ROW");
                string Field(string name)=>fields[columns[name]].Trim('"');
                long Number(string name)=>long.Parse(Field(name),NumberStyles.Integer,CultureInfo.InvariantCulture);
                if(Number("instrument_id")!=instrument.InstrumentId)throw new InvalidDataException("EOD.RECOVERY.WRONG_INSTRUMENT");
                if(columns.ContainsKey("flags") && ((Number("flags")&4)!=0 || (Number("flags")&128)==0))
                    throw new InvalidDataException("EOD.BOOK.GAP_OR_INVALID_EVENT");
                if(instrument.PublisherId>0 && (!columns.ContainsKey("publisher_id") || Number("publisher_id")!=instrument.PublisherId))
                    throw new InvalidDataException("EOD.BOOK.WRONG_PUBLISHER");
                var eventNs=Number("ts_event"); var receiveNs=Number("ts_recv");
                if(eventNs>(boundaryUtc.Ticks-DateTime.UnixEpoch.Ticks)*100 || eventNs<(start.Ticks-DateTime.UnixEpoch.Ticks)*100
                    || receiveNs<eventNs || receiveNs<startNs || receiveNs>=endNs)continue;
                // Do not carry a quote across a later empty/crossed book.
                latest=null;selected=null;
                if(!long.TryParse(Field("ask_px_00"),NumberStyles.Integer,CultureInfo.InvariantCulture,out var ask) || ask<=0 || ask==long.MaxValue)continue;
                var hasBid=long.TryParse(Field("bid_px_00"),NumberStyles.Integer,CultureInfo.InvariantCulture,out var bid) && bid>0 && bid!=long.MaxValue && Number("bid_sz_00")>0;
                if((!hasBid && !instrument.IsOption) || (hasBid && bid>ask) || Number("ask_sz_00")<=0)continue;
                var q=new StoredPositionClosingQuote { ContractId=instrument.ContractId,InstrumentId=instrument.InstrumentId,ValueDate=date,
                    EventTimestampNanoseconds=Number("ts_event"),ReceiveTimestampNanoseconds=Number("ts_recv"),SourceSequence=checked((uint)Number("sequence")),
                    HasBid=hasBid,Bid=hasBid?bid/1_000_000_000m:0,Ask=ask/1_000_000_000m,
                    BidSize=hasBid?checked((uint)Number("bid_sz_00")):0,AskSize=checked((uint)Number("ask_sz_00")) };
                if(q.SourceSequence==0 || q.EventTimestampNanoseconds>(boundaryUtc.Ticks-DateTime.UnixEpoch.Ticks)*100
                    || q.EventTimestampNanoseconds<(boundaryUtc.AddMinutes(-minutes).Ticks-DateTime.UnixEpoch.Ticks)*100
                    || q.ReceiveTimestampNanoseconds<q.EventTimestampNanoseconds
                    || q.ReceiveTimestampNanoseconds<startNs || q.ReceiveTimestampNanoseconds>=endNs)continue;
                if(latest is null || q.EventTimestampNanoseconds>latest.EventTimestampNanoseconds
                    || (q.EventTimestampNanoseconds==latest.EventTimestampNanoseconds && q.ReceiveTimestampNanoseconds>=latest.ReceiveTimestampNanoseconds))
                {latest=q;selected=line;}
            }
            if(latest is null)continue;
            var record=Encoding.UTF8.GetBytes(header+"\n"+selected+"\n");
            latest=latest with {SourceArchiveSha256=Convert.ToHexString(SHA256.HashData(record))};
            latest=latest with {EvidenceId=Convert.ToHexString(SHA256.HashData(TomasAI.IFM.Framework.Serialization.MessagePackBinarySerializer.Shared.Serialize(latest)))};
            return new(latest,record);
        }
        return await AcquireBookAsync(instrument,date,boundaryUtc,key,availableEndNs,token).ConfigureAwait(false);
    }

    static long TimestampNanoseconds(string value)
    {
        // Preserve the provider's nanosecond watermark instead of rounding it up to a .NET tick.
        if(value.Length<20 || !DateTime.TryParseExact(value[..19],["yyyy-MM-dd'T'HH:mm:ss","yyyy-MM-dd HH:mm:ss"],
            CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal,out var seconds))
            throw new InvalidDataException("EOD.RECOVERY.INVALID_AVAILABILITY_TIMESTAMP");
        var index=19;long fraction=0;var digits=0;
        if(value[index]=='.')
        {
            index++;
            while(index<value.Length && value[index] is >= '0' and <= '9')
            { if(++digits>9)throw new InvalidDataException("EOD.RECOVERY.INVALID_AVAILABILITY_TIMESTAMP");fraction=fraction*10+value[index++]-'0'; }
            if(digits==0)throw new InvalidDataException("EOD.RECOVERY.INVALID_AVAILABILITY_TIMESTAMP");
        }
        if(value[index..] is not ("Z" or "+00:00"))throw new InvalidDataException("EOD.RECOVERY.INVALID_AVAILABILITY_TIMESTAMP");
        for(;digits<9;digits++)fraction*=10;
        return checked((seconds.Ticks-DateTime.UnixEpoch.Ticks)*100+fraction);
    }

    static async Task DemandSuccessAsync(HttpResponseMessage response,string operation,CancellationToken token)
    {
        if(response.IsSuccessStatusCode)return;
        // Preserve a bounded provider classification, without logging credentials or full requests.
        var code="unclassified";
        var body=await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
        if(body.Length<=16384)
        {
            try
            {
                using var error=JsonDocument.Parse(body);
                if(error.RootElement.TryGetProperty("detail",out var detail)&&detail.ValueKind==JsonValueKind.Object
                    &&detail.TryGetProperty("case",out var name)&&name.ValueKind==JsonValueKind.String
                    &&name.GetString() is {Length:>0 and <=128} candidate&&candidate.All(c=>char.IsAsciiLetterOrDigit(c)||c=='_'))code=candidate;
            }
            catch(JsonException){ }
        }
        throw new HttpRequestException($"EOD.RECOVERY.PROVIDER_ERROR:{operation}:{(int)response.StatusCode}:{code}",null,response.StatusCode);
    }

    static HttpRequestMessage Create(string method, Dictionary<string,string> parameters,string key)
    {
        var request=new HttpRequestMessage(HttpMethod.Post,new Uri(Endpoint,method));
        request.Headers.Authorization=new AuthenticationHeaderValue("Basic",Convert.ToBase64String(Encoding.UTF8.GetBytes(key+":")));
        request.Content=new FormUrlEncodedContent(parameters);return request;
    }
}
