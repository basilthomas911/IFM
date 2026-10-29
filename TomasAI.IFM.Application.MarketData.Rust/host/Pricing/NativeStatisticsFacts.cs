using System.Buffers;
using System.Text.Json;
using TomasAI.IFM.Application.MarketData.Contracts;
namespace TomasAI.IFM.Application.MarketData.Rust.Host;
/// <summary>Raw saved statistics only. Rust selects membership, interprets date
/// eligibility and owns cancellation/recovery independently of this DB request.</summary>
public sealed class NativeStatisticsFacts(IOptionChainStatisticsStore store)
{
    /// <summary>Copies all source values without financial/date fallback policy.</summary>
    public async Task<byte[]> ReadAsync(JsonElement input,CancellationToken token)
    {
        var contract=input.GetProperty("contract").GetString();var date=DateOnly.FromDayNumber(input.GetProperty("date").GetInt32());var expiry=DateOnly.FromDayNumber(input.GetProperty("expiry").GetInt32());
        if(string.IsNullOrWhiteSpace(contract)||contract.Length>1024||date==default||expiry==default)throw new InvalidDataException("Invalid raw statistics identity.");
        var rows=await store.ReadOptionChainStatisticsAsync(contract,expiry,date,token).ConfigureAwait(false);token.ThrowIfCancellationRequested();
        if(rows.IsDefault||rows.Length>100000)throw new InvalidDataException("Raw option statistics row capacity exceeded.");
        var buffer=new LimitedBuffer();using(var writer=new Utf8JsonWriter(buffer)){
            writer.WriteStartArray();foreach(var row in rows){token.ThrowIfCancellationRequested();writer.WriteStartArray();writer.WriteStringValue(row.ContractId);
                if(row.Volume is{} volume)writer.WriteNumberValue(volume);else writer.WriteNullValue();if(row.VolumeValueDate is{} volumeDate)writer.WriteNumberValue(volumeDate.DayNumber);else writer.WriteNullValue();
                if(row.OpenInterest is{} interest)writer.WriteNumberValue(interest);else writer.WriteNullValue();if(row.OpenInterestValueDate is{} interestDate)writer.WriteNumberValue(interestDate.DayNumber);else writer.WriteNullValue();writer.WriteEndArray();}
            writer.WriteEndArray();writer.Flush();}
        return buffer.Copy();
    }
    sealed class LimitedBuffer:IBufferWriter<byte>
    {
        const int Maximum=4*1024*1024;byte[] bytes=new byte[4096];int written;
        public void Advance(int count){if(count<0||count>bytes.Length-written||written>Maximum-count)throw new InvalidDataException("Raw statistics source exceeds capacity.");written+=count;}
        void Ensure(int size){size=Math.Max(size,1);var required=(long)written+size;if(required>Maximum+65536L)throw new InvalidDataException("Raw statistics reservation exceeds capacity.");if(required>bytes.Length)Array.Resize(ref bytes,(int)Math.Min(Maximum+65536L,Math.Max(required,2L*bytes.Length)));}
        public Memory<byte> GetMemory(int sizeHint=0){Ensure(sizeHint);return bytes.AsMemory(written);}
        public Span<byte> GetSpan(int sizeHint=0){Ensure(sizeHint);return bytes.AsSpan(written);}
        public byte[] Copy()=>bytes.AsSpan(0,written).ToArray();
    }
}
