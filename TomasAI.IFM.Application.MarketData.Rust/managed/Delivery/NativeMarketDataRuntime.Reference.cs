using System.Buffers;
using System.Diagnostics;
using System.Text.Json;
using System.Collections.Immutable;
using TomasAI.IFM.Application.MarketData.Rust.Interop;
using TomasAI.IFM.Shared.MarketData.Contracts;
namespace TomasAI.IFM.Application.MarketData.Rust.Delivery;

public sealed partial class NativeMarketDataRuntime
{
    /// <summary>Supplies the API business clock before a live ValueDate exists.
    /// Reference/administration calls share this anchor with any later live owner;
    /// the native library never fabricates a trading date for those operations.</summary>
    public void ConfigureApiClock(DateTimeOffset apiUtcTime,bool advanceClock=true)
    {lock(gate){EnsureLiveAccess();NativeMarketDataLibrary.Check(library.ApiClock(handle.Identity,apiUtcTime.UtcTicks-DateTimeOffset.UnixEpoch.UtcTicks,advanceClock?1u:0),"configure API clock");}}
    /// <summary>Runs the qualified pure conversion without I/O, allocations or
    /// asynchronous blocking; source/unit validation belongs to application policy.</summary>
    public double ConvertTreasuryHalfYield(double halfYield)
    {
        lock(gate){EnsureLiveAccess();NativeMarketDataLibrary.Check(library.TreasuryRate(halfYield,out var rate),"convert Treasury rate");return rate;}
    }
    readonly HashSet<ulong> referenceOperations=[];
    /// <summary>Configures official Treasury acquisition after the API clock.
    /// DNS endpoints are numeric and no Databento credential is sent to Treasury.</summary>
    public unsafe void ConfigureTreasury(string endpoint,string[] numericDnsServers)
    {
        var data=JsonSerializer.SerializeToUtf8Bytes(new{endpoint,dns=numericDnsServers});
        if(data.Length>8192)throw new ArgumentOutOfRangeException(nameof(endpoint));
        lock(gate){EnsureLiveAccess();fixed(byte* input=data)NativeMarketDataLibrary.Check(library.ReferenceConfigure(handle.Identity,input,(uint)data.Length),"configure Treasury");}
    }
    /// <summary>Returns exact official-source dates, decimal percentages and API
    /// retrieval timestamps. All network/cache/parse work runs in native ownership.</summary>
    public Task<TreasuryCurveSnapshot[]> ReadTreasuryCurvesAsync(DateOnly from,DateOnly to,TimeSpan remainingBudget,CancellationToken cancellationToken=default)
        =>ReadReferenceAsync(0,from,to,remainingBudget,cancellationToken);
    /// <summary>Preserves the current inclusive thirteen-day source lookback.</summary>
    public async Task<TreasuryCurveSnapshot?> ReadTreasuryCurveAsync(DateOnly asOf,TimeSpan remainingBudget,CancellationToken cancellationToken=default)
    {
        var rows=await ReadReferenceAsync(1,asOf,asOf,remainingBudget,cancellationToken).ConfigureAwait(false);
        if(rows.Length>1)throw new InvalidDataException("Native latest Treasury result contains multiple rows.");return rows.FirstOrDefault();
    }
    static uint ReferenceDate(DateOnly value)=>checked((uint)(value.Year*10000+value.Month*100+value.Day));
    (ulong Operation,Task Completed) SubmitReference(uint mode,DateOnly from,DateOnly to,TimeSpan budget,CancellationToken token)
    {
        lock(gate)
        {
            EnsureLiveAccess();token.ThrowIfCancellationRequested();var operation=checked(++nextOperation);
            var request=new Pending{Control=new(TaskCreationOptions.RunContinuationsAsynchronously)};pending.Add(operation,request);
            var code=library.ReferenceSubmit(handle.Identity,operation,mode,ReferenceDate(from),ReferenceDate(to),(ulong)Math.Ceiling(budget.TotalMilliseconds));
            if(code!=0){pending.Remove(operation);NativeMarketDataLibrary.Check(code,"submit Treasury");}
            referenceOperations.Add(operation);request.Cancellation=token.UnsafeRegister(_=>library.Cancel(handle.Identity,operation),null);
            return(operation,request.Control.Task);
        }
    }
    async Task<TreasuryCurveSnapshot[]> ReadReferenceAsync(uint mode,DateOnly from,DateOnly to,TimeSpan budget,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();if(budget<TimeSpan.Zero)throw new ArgumentOutOfRangeException(nameof(budget));
        if(budget>TimeSpan.FromMinutes(2))budget=TimeSpan.FromMinutes(2);var started=Stopwatch.GetTimestamp();
        var request=SubmitReference(mode,from,to,budget,token);
        try
        {
            await request.Completed.ConfigureAwait(false);token.ThrowIfCancellationRequested();
            if(Stopwatch.GetElapsedTime(started)>=budget)throw new TimeoutException("Original Treasury acquisition deadline expired.");
            return TakeReference(request.Operation);
        }
        finally
        {
            lock(gate)if(referenceOperations.Remove(request.Operation))NativeMarketDataLibrary.Check(library.ReferenceDiscard(handle.Identity,request.Operation),"discard Treasury result");
        }
    }
    unsafe TreasuryCurveSnapshot[] TakeReference(ulong operation)
    {
        var buffer=ArrayPool<byte>.Shared.Rent(524_288);
        try
        {
            uint length;lock(gate){EnsureLiveAccess();fixed(byte* output=buffer)NativeMarketDataLibrary.Check(library.ReferenceTake(handle.Identity,operation,output,524_288,out length),"copy Treasury result");}
            if(length>524_288)throw new InvalidDataException("Native Treasury result exceeded capacity.");
            using var document=JsonDocument.Parse(buffer.AsMemory(0,checked((int)length)));var root=document.RootElement;
            if(root.ValueKind!=JsonValueKind.Array||root.GetArrayLength()>366)throw new InvalidDataException("Invalid native Treasury result.");
            var rows=new List<TreasuryCurveSnapshot>();
            foreach(var row in root.EnumerateArray())
            {
                var date=row.GetProperty("date").GetInt32();var points=new List<TreasuryRatePoint>();
                if(row.GetProperty("source").GetString()!="USTreasury"||row.GetProperty("rates").GetArrayLength()>12)throw new InvalidDataException("Invalid Treasury source identity.");
                foreach(var point in row.GetProperty("rates").EnumerateArray())
                {
                    var tenor=(TreasuryTenor)point.GetProperty("months").GetUInt16();if(!Enum.IsDefined(tenor))throw new InvalidDataException("Invalid Treasury tenor.");
                    var words=point.GetProperty("bits");if(words.GetArrayLength()!=4)throw new InvalidDataException("Invalid Treasury decimal words.");
                    points.Add(new(tenor,new decimal(words.EnumerateArray().Select(x=>unchecked((int)x.GetUInt32())).ToArray())));
                }
                rows.Add(new(new(date/10000,date/100%100,date%100),points.ToImmutableArray(),DateTimeOffset.UnixEpoch.AddTicks(row.GetProperty("retrieved").GetInt64()),"USTreasury"));
            }
            return rows.ToArray();
        }
        finally{ArrayPool<byte>.Shared.Return(buffer,true);}
    }
    void DiscardReferences()
    {
        lock(gate){foreach(var operation in referenceOperations)NativeMarketDataLibrary.Check(library.ReferenceDiscard(handle.Identity,operation),"discard retained Treasury");referenceOperations.Clear();}
    }
}
