namespace TomasAI.IFM.Application.Api.Server.Core.Hosting.Modes;

/// <summary>Historical business UTC with unchanged system monotonic timestamps and physical timers.</summary>
public sealed class HistoricalReplayClock : TimeProvider,TomasAI.IFM.Shared.Time.IRecordedRealtimeSourceReplay,TomasAI.IFM.Shared.Time.IRecordedRealtimeGenerationAuthority
{
    readonly object gate=new();
    DateTimeOffset anchor;
    DateTimeOffset lastRecorded;
    long anchoredAt;
    bool running;
    TomasAI.IFM.Shared.Time.IRecordedRealtimeGenerationAuthority? generationAuthority;
    public bool UsesRecordedSource(string actor,string verb)=>(actor,verb) is
        ("FuturesTradeSessionBarSignal","Barrier") or
        ("FuturesTradePositionRealtime","EvaluateFuturesExitClock") or
        ("IronCondorTradePositionRealtime","IronCondorMonitoringInputsChanged");
    public void SetGenerationAuthority(TomasAI.IFM.Shared.Time.IRecordedRealtimeGenerationAuthority authority)
    {
        ArgumentNullException.ThrowIfNull(authority);
        if(Interlocked.CompareExchange(ref generationAuthority,authority,null) is{} current && !ReferenceEquals(current,authority))throw new InvalidOperationException("Original generation authority is immutable for this replay host.");
    }
    public bool Allows(TomasAI.IFM.Shared.Time.RecordedRealtimeGenerationAcceptance original,Guid currentGenerationId)
        =>currentGenerationId!=Guid.Empty&&UsesRecordedSource(original.Subject.Name,original.Subject.Verb)&&Volatile.Read(ref generationAuthority)?.Allows(original,currentGenerationId)==true;
    public bool IsRunning {get{lock(gate)return running;}}
    public HistoricalReplayClock(DateTimeOffset initialUtc)
    {
        anchor=lastRecorded=initialUtc.ToUniversalTime();anchoredAt=GetTimestamp();
    }
    public override DateTimeOffset GetUtcNow()
    {
        lock(gate)return running?anchor+GetElapsedTime(anchoredAt):anchor;
    }
    public void AdvanceTo(DateTimeOffset utc)
    {
        lock(gate)
        {
            var current=running?anchor+GetElapsedTime(anchoredAt):anchor;
            if(utc<lastRecorded)throw new InvalidOperationException("Historical replay recorded arrivals cannot move backwards.");
            lastRecorded=utc.ToUniversalTime();
            if(!running||utc>current){anchor=utc.ToUniversalTime();anchoredAt=GetTimestamp();running=true;}
        }
    }
    // Intentionally inherit GetTimestamp, TimestampFrequency and CreateTimer from TimeProvider.System.
    // Advancing a tape must never extend financial/decision budgets or synthesize elapsed processing time.
}
