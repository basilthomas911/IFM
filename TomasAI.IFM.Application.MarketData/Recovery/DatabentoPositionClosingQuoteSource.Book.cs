using System.Globalization;
using System.Text;
using System.Security.Cryptography;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Framework.Serialization;
namespace TomasAI.IFM.Application.MarketData.Recovery;

public sealed partial class DatabentoPositionClosingQuoteSource
{
    async Task<AcquiredPositionClosingQuote?> AcquireBookAsync(PositionClosingInstrument instrument,DateOnly date,
        DateTime boundary,string key,long availableEndNs,CancellationToken token)
    {
        // Databento record counts are exact only on ten-minute boundaries.
        var end=boundary.AddMinutes(10);
        if(availableEndNs<(end.Ticks-DateTime.UnixEpoch.Ticks)*100)return null;
        foreach(var start in new[]{boundary.AddHours(-1),boundary.AddHours(-2),boundary.Date}.Distinct())
        {
            var parameters=new Dictionary<string,string>{["dataset"]=instrument.Dataset,["schema"]="mbp-1",
                ["symbols"]=instrument.RawSymbol ?? instrument.InstrumentId.ToString(CultureInfo.InvariantCulture),["stype_in"]=instrument.RawSymbol is null ? "instrument_id" : "raw_symbol",
                ["start"]=start.ToString("O",CultureInfo.InvariantCulture),["end"]=end.ToString("O",CultureInfo.InvariantCulture)};
            using(var costRequest=Create("metadata.get_cost",parameters,key))
            using(var costResponse=await http.SendAsync(costRequest,token).ConfigureAwait(false))
            {
                await DemandSuccessAsync(costResponse,"metadata.get_cost",token).ConfigureAwait(false);
                var costText=await costResponse.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                if(costText.Length>128 || !decimal.TryParse(costText.Trim().Trim('"'),NumberStyles.Float,CultureInfo.InvariantCulture,out var cost)
                    || cost<0 || cost>options.MaximumRequestCostUsd)throw new InvalidOperationException("EOD.RECOVERY.HISTORICAL_COST_NOT_AUTHORIZED");
            }
            long expected;
            using(var countRequest=Create("metadata.get_record_count",parameters,key))
            using(var countResponse=await http.SendAsync(countRequest,token).ConfigureAwait(false))
            {
                await DemandSuccessAsync(countResponse,"metadata.get_record_count",token).ConfigureAwait(false);
                var countText=await countResponse.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                if(countText.Length>128 || !long.TryParse(countText.Trim().Trim('"'),NumberStyles.Integer,CultureInfo.InvariantCulture,out expected)
                    || expected<0 || expected>options.MaximumResponseBytes/32)throw new InvalidDataException("EOD.BOOK.INVALID_RECORD_COUNT");
            }
            if(expected==0)continue;
            parameters["encoding"]="csv";parameters["compression"]="none";parameters["pretty_px"]="false";
            parameters["pretty_ts"]="false";parameters["map_symbols"]="false";
            using var request=Create("timeseries.get_range",parameters,key);
            using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token).ConfigureAwait(false);
            await DemandSuccessAsync(response,"timeseries.get_range",token).ConfigureAwait(false);
            if(response.Content.Headers.ContentLength>options.MaximumResponseBytes)throw new InvalidDataException("EOD.RECOVERY.RESPONSE_TOO_LARGE");
            await using var stream=await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            using var reader=new StreamReader(stream,Encoding.UTF8,false,4096,true);
            using var archive=new MemoryStream();
            using(var writer=new StreamWriter(archive,new UTF8Encoding(false),4096,true){NewLine="\n"})
            {
                long bytes=0;
                while(await reader.ReadLineAsync(token).ConfigureAwait(false) is {} line)
                {
                    bytes+=Encoding.UTF8.GetByteCount(line)+1;
                    if(bytes>options.MaximumResponseBytes || line.Length>16384)throw new InvalidDataException("EOD.RECOVERY.RESPONSE_TOO_LARGE");
                    await writer.WriteLineAsync(line.AsMemory(),token).ConfigureAwait(false);
                }
                await writer.FlushAsync(token).ConfigureAwait(false);
            }
            var raw=archive.ToArray();
            var proof=new VerifiedClosingBook{Dataset=instrument.Dataset,RangeStartUtc=start,RangeEndUtc=end,
                VerifiedThroughUtc=boundary,CapturedAtUtc=DateTime.UtcNow,ExpectedRecords=expected,PublisherId=instrument.PublisherId};
            StoredPositionClosingQuote? quote;
            try { quote=ClosingBookArchive.Reconstruct(instrument.ContractId,instrument.InstrumentId,date,boundary,proof,raw,instrument.IsOption); }
            catch (InvalidDataException error)
            {
                // Retain the validation failure and enough source identity to distinguish a provider
                // count mismatch from a wrong reference publisher. Never substitute an older quote.
                var rows=raw.Count(value=>value==(byte)'\n')-1;
                throw new InvalidDataException($"{error.Message};Contract={instrument.ContractId};Instrument={instrument.InstrumentId};Publisher={instrument.PublisherId};Expected={expected};Rows={rows};Start={start:O};End={end:O}",error);
            }
            if(quote is null)continue;
            quote=quote with{EvidenceId=Convert.ToHexString(SHA256.HashData(MessagePackBinarySerializer.Shared.Serialize(quote)))};
            return new(quote,raw);
        }
        return null;
    }
}
