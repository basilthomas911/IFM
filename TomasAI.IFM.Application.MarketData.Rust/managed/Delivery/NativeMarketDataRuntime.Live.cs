using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using TomasAI.IFM.Application.MarketData.Rust.Interop;
namespace TomasAI.IFM.Application.MarketData.Rust.Delivery;

/// <summary>Independent logical dataset owners within the single native runtime.</summary>
public enum MarketDataLane:uint { /// <summary>GLBX futures.</summary>
    FuturesGlbx=1, /// <summary>XCBF futures.</summary>
    FuturesXcbf=2, /// <summary>GLBX options, isolated from futures.</summary>
    OptionsGlbx=3 }
/// <summary>Explicit provider subscription below the public application API.</summary>
public sealed record NativeSubscription(string[] Symbols,string Schema,string Symbology,long? ReplayStartNanoseconds=null);
/// <summary>Application-qualified canonical/provider mapping and aggregate policy.</summary>
public sealed record NativeRegistration(uint Instrument,string Contract,string Source,bool Option,long? Tick,bool Vwap,bool Required,ushort Publisher=0);
/// <summary>Copied native status. Readiness here proves native processing only.</summary>
public sealed record LiveLaneStatus(uint Phase,ulong Attempt,ulong Episode,ulong Attempts,Guid Generation,string? Error);
/// <summary>Copied exact raw observation. Prices are nanounits; times UTC ticks since Unix epoch.</summary>
public readonly record struct LiveObservation(uint Flags,Guid Generation,long Sequence,long EventTicks,long ReceiveTicks,long Price,uint Quantity,long Bid,long Ask,uint BidQuantity,uint AskQuantity,long LocalTicks,uint BidCount,uint AskCount);

public sealed partial class NativeMarketDataRuntime {
    DateOnly? liveValueDate;
    /// <summary>The configured API ValueDate, never inferred from the machine clock.</summary>
    public DateOnly? LiveValueDate {get {lock(gate){EnsureLiveAccess();return liveValueDate;}}}
    static readonly UTF8Encoding StrictUtf8=new(false,true);
    static Guid Identity(ulong low,ulong high){Span<byte> bytes=stackalloc byte[16];BinaryPrimitives.WriteUInt64LittleEndian(bytes,low);BinaryPrimitives.WriteUInt64LittleEndian(bytes[8..],high);return new(bytes);}
    /// <summary>Configures one API-supplied ValueDate and business clock. Does not connect or infer local dates.</summary>
    public void ConfigureLive(DateOnly valueDate,Guid identity,DateTimeOffset apiUtcTime,bool advanceClock=true,uint capacity=2048) {
        Span<byte> bytes=stackalloc byte[16];identity.TryWriteBytes(bytes);
        var config=new NativeLiveConfiguration{Size=40,Date=(uint)(valueDate.Year*10000+valueDate.Month*100+valueDate.Day),UtcTicks=apiUtcTime.UtcTicks-DateTimeOffset.UnixEpoch.Ticks,IdentityLow=BinaryPrimitives.ReadUInt64LittleEndian(bytes),IdentityHigh=BinaryPrimitives.ReadUInt64LittleEndian(bytes[8..]),Capacity=capacity,Advancing=advanceClock?1u:0};
        lock(gate){EnsureLiveAccess();NativeMarketDataLibrary.Check(library.ConfigureLive(handle.Identity,in config),"configure live");liveValueDate=valueDate;}
    }
    /// <summary>Updates the explicit API clock without altering ValueDate.</summary>
    public void UpdateLiveClock(DateTimeOffset apiUtcTime,bool advanceClock=true){lock(gate){EnsureLiveAccess();NativeMarketDataLibrary.Check(library.ClockLive(handle.Identity,apiUtcTime.UtcTicks-DateTimeOffset.UnixEpoch.Ticks,advanceClock?1u:0),"update live clock");}}
    void EnsureLiveAccess(){ObjectDisposedException.ThrowIf(disposal is not null,this);if(deliveryFailure is not null)throw new InvalidOperationException("Native completion delivery failed.",deliveryFailure);}
    /// <summary>Starts a real native lane. Control serialization occurs once, not per tick. Credentials never enter diagnostics.</summary>
    public Task StartLiveLaneAsync(MarketDataLane lane,string key,string host,ushort port,string[] numericDnsServers,NativeSubscription[] subscriptions,NativeRegistration[] registrations,bool requireObservations,TimeSpan remainingBudget,CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(subscriptions);ArgumentNullException.ThrowIfNull(registrations);
        if(remainingBudget<TimeSpan.Zero)throw new ArgumentOutOfRangeException(nameof(remainingBudget));
        if(remainingBudget>TimeSpan.FromMinutes(2))remainingBudget=TimeSpan.FromMinutes(2);
        // Bound before serialization too; the native side independently checks 2 MiB.
        if(subscriptions.Length is <1 or >64||registrations.Length is <1 or >2048)throw new ArgumentOutOfRangeException(nameof(registrations));
        var started=System.Diagnostics.Stopwatch.GetTimestamp();
        var data=JsonSerializer.SerializeToUtf8Bytes(new{key,host,port,dns=numericDnsServers,evidence=requireObservations?"observations":"control",subscriptions=subscriptions.Select(s=>new{symbols=s.Symbols,schema=s.Schema,symbology=s.Symbology,replay_start_ns=s.ReplayStartNanoseconds}),registrations=registrations.Select(r=>new{instrument=r.Instrument,publisher=r.Publisher,contract=r.Contract,source=r.Source,asset=r.Option?"option":"futures",tick=r.Tick,vwap=r.Vwap,required=r.Required})});
        try{var remaining=remainingBudget-System.Diagnostics.Stopwatch.GetElapsedTime(started);return SubmitLiveAsync(lane,0,data,remaining>TimeSpan.Zero?remaining:TimeSpan.Zero,cancellationToken);}finally{System.Security.Cryptography.CryptographicOperations.ZeroMemory(data);}
    }
    /// <summary>Replaces only this lane within the caller's original remaining recovery budget.</summary>
    public Task ResetLiveLaneAsync(MarketDataLane lane,TimeSpan remainingBudget,CancellationToken cancellationToken=default)=>SubmitLiveAsync(lane,1,[],remainingBudget,cancellationToken);
    /// <summary>Signals this lane before waiting for joined provider cleanup.</summary>
    public Task StopLiveLaneAsync(MarketDataLane lane,TimeSpan remainingBudget,CancellationToken cancellationToken=default)=>SubmitLiveAsync(lane,2,[],remainingBudget,cancellationToken);
    unsafe Task SubmitLiveAsync(MarketDataLane lane,uint action,byte[] data,TimeSpan budget,CancellationToken cancellationToken){
        cancellationToken.ThrowIfCancellationRequested();if(budget<TimeSpan.Zero)throw new ArgumentOutOfRangeException(nameof(budget));if(data.Length>2*1024*1024)throw new ArgumentOutOfRangeException(nameof(data));
        lock(gate){EnsureLiveAccess();var operation=checked(++nextOperation);var request=new Pending{Control=new(TaskCreationOptions.RunContinuationsAsynchronously)};pending.Add(operation,request);
            int status;fixed(byte* input=data)status=library.SubmitLive(handle.Identity,operation,action,(uint)lane,input,(uint)data.Length,(ulong)Math.Ceiling(Math.Min(budget.TotalMilliseconds,120_000)));
            if(status!=0){pending.Remove(operation);NativeMarketDataLibrary.Check(status,"submit live");}
            request.Cancellation=cancellationToken.UnsafeRegister(_=>library.Cancel(handle.Identity,operation),null);return request.Control.Task;
        }
    }
    /// <summary>Reads native lifecycle immediately. A healthy transport alone does not prove downstream actor recovery.</summary>
    public unsafe LiveLaneStatus ReadLiveStatus(MarketDataLane lane){lock(gate){EnsureLiveAccess();NativeMarketDataLibrary.Check(library.StatusLive(handle.Identity,(uint)lane,64,out var result),"read live status");if(result.Size!=64||result.Phase>5||result.Reserved0!=0||result.Reserved1!=0)throw new InvalidDataException("Malformed native live status.");NativeMarketDataLibrary.Check(library.FailureLive(handle.Identity,(uint)lane,4112,out var failure),"read lane failure");if(failure.Size!=4112||failure.Length>4096||failure.Failure>2)throw new InvalidDataException("Malformed native lane failure.");var error=failure.Failure==0?null:StrictUtf8.GetString(new ReadOnlySpan<byte>(failure.Message,(int)failure.Length));return new(result.Phase,result.Attempt,result.Episode,result.Attempts,Identity(result.GenerationLow,result.GenerationHigh),error);}}
    /// <summary>Reads raw last trade/quote in exact source units; flags zero means unavailable.</summary>
    public unsafe LiveObservation ReadLiveObservation(MarketDataLane lane,string contract,bool quote=false){
        ArgumentNullException.ThrowIfNull(contract);if(contract.Length is <1 or >256)throw new ArgumentOutOfRangeException(nameof(contract));Span<byte> bytes=stackalloc byte[1024];var length=StrictUtf8.GetBytes(contract,bytes);if(length>256)throw new ArgumentOutOfRangeException(nameof(contract));
        lock(gate){EnsureLiveAccess();NativeLast result;fixed(byte* id=bytes)NativeMarketDataLibrary.Check(library.LastLive(handle.Identity,(uint)lane,quote?2u:1,id,(uint)length,104,out result),"read live observation");if(result.Size!=104||result.Reserved!=0||result.Flags>7)throw new InvalidDataException("Malformed native observation.");return new(result.Flags,Identity(result.GenerationLow,result.GenerationHigh),result.Sequence,result.EventTicks,result.ReceiveTicks,result.Price,result.Quantity,result.Bid,result.Ask,result.BidQuantity,result.AskQuantity,result.LocalTicks,result.BidCount,result.AskCount);}
    }
    unsafe void CompleteControl(Pending request,NativeCompletion record){
        var completion=request.Control!;
        try{NativeMarketDataLibrary.Check(library.TakeLive(handle.Identity,record.Operation,4112,out var result),"take live result");if(result.Size!=4112||result.Length>4096||result.Failure>2||result.Reserved!=0)throw new InvalidDataException("Malformed native control result.");var message=StrictUtf8.GetString(new ReadOnlySpan<byte>(result.Message,(int)result.Length));
            switch(record.Outcome){case 0:if(result.Failure==0)completion.TrySetResult();else completion.TrySetException(new InvalidOperationException(message));break;case 1:completion.TrySetCanceled();break;case 2:completion.TrySetException(new TimeoutException("Native market-data recovery deadline expired."));break;default:completion.TrySetException(new InvalidOperationException("Native market-data task failed internally."));break;}
        }catch(Exception error){completion.TrySetException(error);throw;}
    }
}

/// <summary>Blittable exact source contribution. Payload slots are documented in ABI-OWNERSHIP.md.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
public unsafe struct LiveMarketEvent {
    /// <summary>128-byte wire layout identity.</summary>
    public uint Size;
    /// <summary>Logical owner, 1..3.</summary>
    public MarketDataLane Lane;
    /// <summary>1 quote, 2 trade, 3 order, 4 statistic, 5 trade replay completion, 6 statistics replay completion.</summary>
    public uint Kind;
    /// <summary>Provider instrument identity.</summary>
    public uint Instrument;
    /// <summary>Opaque owner identity low bits.</summary>
    public ulong GenerationLow;
    /// <summary>Opaque owner identity high bits.</summary>
    public ulong GenerationHigh;
    /// <summary>12 exact signed slots; no floating point price conversion.</summary>
    public fixed long Fields[12];
}
/// <summary>Full redacted provider diagnostic, preserving unknown codes and batch markers.</summary>
public sealed record LiveDiagnostic(MarketDataLane Lane,uint Kind,uint Code,ulong Attempt,bool Last,string Message);

public sealed partial class NativeMarketDataRuntime {
    readonly System.Threading.Channels.Channel<byte> livePulse=System.Threading.Channels.Channel.CreateBounded<byte>(new System.Threading.Channels.BoundedChannelOptions(1){SingleReader=true,SingleWriter=true,FullMode=System.Threading.Channels.BoundedChannelFullMode.DropWrite,AllowSynchronousContinuations=false});
    int liveBits;
    void PumpLiveSignal(){NativeMarketDataLibrary.Check(library.SignalsLive(handle.Identity,out var bits),"take live signal");if((bits&32)!=0){SignalHistoricalReaders();bits&=~32u;}if((bits&64)!=0){SignalHistoricalInputs();bits&=~64u;}if(bits!=0){Interlocked.Or(ref liveBits,(int)bits);livePulse.Writer.TryWrite(1);}}
    /// <summary>Single-consumer event-driven wake-up. Bits 1..3 identify lanes and bit 4 identifies HTTP warnings with
    /// changes; authoritative data and diagnostics remain in their separate buffers.
    /// Drain until caught up after each wake-up. No timer or request polling occurs.</summary>
    public async ValueTask<uint> WaitForLiveChangesAsync(CancellationToken cancellationToken=default){await livePulse.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);return (uint)Interlocked.Exchange(ref liveBits,0);}
    /// <summary>Copies at most 64 exact records into caller-owned storage without
    /// per-record allocation. Retain and publish all authoritative contributions;
    /// the generation identifies data superseded by emergency recovery.</summary>
    public unsafe int DrainLiveEvents(Span<LiveMarketEvent> destination){if(destination.Length>64)throw new ArgumentOutOfRangeException(nameof(destination));lock(gate){EnsureLiveAccess();uint count;fixed(LiveMarketEvent* records=destination)NativeMarketDataLibrary.Check(library.EventsLive(handle.Identity,records,(uint)destination.Length,out count),"drain live events");if(count>destination.Length)throw new InvalidDataException("Native output exceeded capacity.");for(var i=0;i<count;i++){if(destination[i].Size!=128||destination[i].Kind is <1 or >6)throw new InvalidDataException("Malformed native market event.");}return (int)count;}}
    /// <summary>Reads a bounded batch of full diagnostic text. Tail messages remain
    /// buffered for the next drain; this does not consume market records.</summary>
    public unsafe LiveDiagnostic[] DrainLiveDiagnostics(){lock(gate){EnsureLiveAccess();var buffer=System.Buffers.ArrayPool<NativeDiagnostic>.Shared.Rent(64);try{uint count;fixed(NativeDiagnostic* records=buffer)NativeMarketDataLibrary.Check(library.DiagnosticsLive(handle.Identity,records,64,out count),"drain live diagnostics");if(count>64)throw new InvalidDataException("Native diagnostic batch exceeded capacity.");var result=new LiveDiagnostic[count];for(var i=0;i<count;i++){ref var row=ref buffer[i];if(row.Size!=4128||row.Length>4096||row.Kind is <1 or >2||row.Code>255||row.Last>1)throw new InvalidDataException("Malformed native diagnostic.");fixed(byte* message=row.Message)result[i]=new((MarketDataLane)row.Lane,row.Kind,row.Code,row.Attempt,row.Last==1,StrictUtf8.GetString(new ReadOnlySpan<byte>(message,(int)row.Length)));}return result;}finally{System.Buffers.ArrayPool<NativeDiagnostic>.Shared.Return(buffer,true);}}}
}
