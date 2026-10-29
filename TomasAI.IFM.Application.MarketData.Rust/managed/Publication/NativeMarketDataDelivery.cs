using System.Diagnostics;
using System.Threading.Channels;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Application.MarketData.Rust.OptionChains;

namespace TomasAI.IFM.Application.MarketData.Rust.Publication;

/// <summary>Single runtime signal consumer. Native queues buffer catch-up only;
/// no periodic polling or wait-for-batch is used. Latest quote publication and
/// ordered trade/evidence work have independent, bounded wake-up channels.
/// Required application observers own logging and the existing recovery policy.</summary>
public sealed class NativeMarketDataDelivery : IAsyncDisposable
{
    readonly NativeMarketDataRuntime runtime;
    readonly NativeOptionChainRuntime scopes;
    readonly NativeOptionQuoteDelivery quotes;
    readonly NativeOptionTradeDelivery trades;
    readonly Func<uint,CancellationToken,ValueTask> primary;
    readonly Action<LiveDiagnostic> diagnostic;
    readonly Action<HistoricalWarning> warning;
    readonly Action<string,LiveLaneStatus> status;
    readonly Action<string?,Exception> fault;
    readonly CancellationTokenSource stopping=new();
    readonly Channel<byte> quotePulse=Pulse(),tradePulse=Pulse(),primaryPulse=Pulse();
    readonly Task receiver,quoteWorker,tradeWorker,primaryWorker;
    readonly object disposalGate=new();
    Task? disposal,primaryCallback;
    int primaryBits;uint primaryCallbackMask;
    readonly Dictionary<string,LiveLaneStatus> lifecycleStates=new(StringComparer.Ordinal);
    System.Collections.Immutable.ImmutableArray<string> lifecycleScopes;
    int joined,receiveFailed;
    readonly SemaphoreSlim optionObservation=new(1);
    /// <summary>True only after every owned loop and actual callback has joined,
    /// including shutdown that subsequently reports an earlier observer error.</summary>
    public bool IsJoined=>Volatile.Read(ref joined)!=0;
    static Channel<byte> Pulse()=>Channel.CreateBounded<byte>(new BoundedChannelOptions(1){SingleReader=true,SingleWriter=true,
        FullMode=BoundedChannelFullMode.DropWrite,AllowSynchronousContinuations=false});

    /// <summary>Starts owned receivers after native/application scope preparation.
    /// Observers are synchronous, nonblocking and must report every unknown
    /// failure. Primary handles non-option lane changes; status drives application
    /// recovery/readiness, never equating native readiness with actor proof.</summary>
    public NativeMarketDataDelivery(NativeMarketDataRuntime runtime,NativeOptionChainRuntime scopes,NativeOptionQuoteDelivery quotes,
        NativeOptionTradeDelivery trades,Func<uint,CancellationToken,ValueTask> primary,Action<LiveDiagnostic> diagnostic,
        Action<HistoricalWarning> warning,Action<string,LiveLaneStatus> status,Action<string?,Exception> fault)
    {
        this.runtime=runtime??throw new ArgumentNullException(nameof(runtime));this.scopes=scopes??throw new ArgumentNullException(nameof(scopes));
        this.quotes=quotes??throw new ArgumentNullException(nameof(quotes));this.trades=trades??throw new ArgumentNullException(nameof(trades));
        this.primary=primary??throw new ArgumentNullException(nameof(primary));this.diagnostic=diagnostic??throw new ArgumentNullException(nameof(diagnostic));
        this.warning=warning??throw new ArgumentNullException(nameof(warning));this.status=status??throw new ArgumentNullException(nameof(status));
        this.fault=fault??throw new ArgumentNullException(nameof(fault));
        receiver=ReceiveAsync();quoteWorker=QuotesAsync();tradeWorker=TradesAsync();primaryWorker=PrimaryAsync();
    }

    /// <summary>Every required delivery loop is still receiving/processing.
    /// WhenAll alone can hide a dead receiver behind a blocked remaining worker.</summary>
    public bool IsOperational=>Volatile.Read(ref receiveFailed)==0&&!stopping.IsCancellationRequested&&!receiver.IsCompleted&&!quoteWorker.IsCompleted&&!tradeWorker.IsCompleted&&!primaryWorker.IsCompleted;
    /// <summary>True when shared receive/delivery ownership needs replacement.</summary>
    public bool NeedsReplacement=>Volatile.Read(ref receiveFailed)!=0||receiver.IsCompleted||quoteWorker.IsCompleted||tradeWorker.IsCompleted||primaryWorker.IsCompleted;
    /// <summary>Completion of all required loops, preserving any observer fault.</summary>
    public Task Completion=>Task.WhenAll(receiver,quoteWorker,tradeWorker,primaryWorker);

    async Task ReceiveAsync()
    {
        try
        {
            while(true)
            {
                var bits=await runtime.WaitForLiveChangesAsync(stopping.Token).ConfigureAwait(false);
                LiveDiagnostic[] batch;
                do{batch=runtime.DrainLiveDiagnostics();foreach(var item in batch)diagnostic(item);}while(batch.Length==64);
                if((bits&16)!=0)
                {
                    HistoricalWarning[] warnings;
                    do{warnings=runtime.DrainHistoricalWarnings();foreach(var item in warnings)warning(item);}while(warnings.Length==16);
                }
                if((bits&8)!=0)
                {
                    // Reset owns option status temporarily. Do not wait here:
                    // the shared signal receiver must continue futures and
                    // diagnostics even while an option callback is blocked.
                    if(optionObservation.Wait(0))
                    {try{ObserveOptionStatus();}finally{optionObservation.Release();}}
                    quotePulse.Writer.TryWrite(1);tradePulse.Writer.TryWrite(1);
                }
                if((bits&~24u)!=0){Interlocked.Or(ref primaryBits,(int)(bits&~24u));primaryPulse.Writer.TryWrite(1);}
            }
        }
        catch(OperationCanceledException)when(stopping.IsCancellationRequested){}
        catch(Exception error){Volatile.Write(ref receiveFailed,1);fault(null,new NativeDeliveryFailure("Receiver",3,error));throw;}
        finally{quotePulse.Writer.TryComplete();tradePulse.Writer.TryComplete();primaryPulse.Writer.TryComplete();}
    }

    void ObserveOptionStatus()
    {
        if(runtime.ReadOptionScopeFailure() is{} failed)fault(null,new InvalidOperationException(failed));
        var active=scopes.ActiveScopes;
        if(!lifecycleScopes.Equals(active))
        {foreach(var retired in lifecycleStates.Keys.Where(id=>!active.Contains(id)).ToArray())lifecycleStates.Remove(retired);lifecycleScopes=active;}
        foreach(var id in active)if(runtime.TryReadOptionScopeStatusChange(id,lifecycleStates.GetValueOrDefault(id),out var changed))
        {lifecycleStates[id]=changed!;status(id,changed!);}
    }

    async Task QuotesAsync()
    {
        try{await foreach(var pulse in quotePulse.Reader.ReadAllAsync(stopping.Token))
            try{await quotes.PublishAvailableAsync(TimeSpan.FromMinutes(2),stopping.Token).ConfigureAwait(false);}
            catch(OperationCanceledException)when(stopping.IsCancellationRequested){throw;}
            catch(Exception error){fault(null,new NativeDeliveryFailure("OptionQuote",0,error));}}
        catch(OperationCanceledException)when(stopping.IsCancellationRequested){}
    }
    async Task TradesAsync()
    {
        try{await foreach(var pulse in tradePulse.Reader.ReadAllAsync(stopping.Token))
            foreach(var id in scopes.ActiveScopes)
                try{while(await trades.DrainAvailableAsync(id,TimeSpan.FromMinutes(2),stopping.Token).ConfigureAwait(false)==256)
                    await Task.Yield();}
                catch(OperationCanceledException)when(stopping.IsCancellationRequested){throw;}
                catch(Exception error){fault(id,new NativeDeliveryFailure("OptionTrade",0,error));}}
        catch(OperationCanceledException)when(stopping.IsCancellationRequested){}
    }
    async Task PrimaryAsync()
    {
        try{await foreach(var pulse in primaryPulse.Reader.ReadAllAsync(stopping.Token))
            try
            {
                if(primaryCallback is {IsCompleted:false})throw new InvalidOperationException("Previous primary publication callback has not joined.");
                _=primaryCallback?.Exception;
                var bits=(uint)Interlocked.Exchange(ref primaryBits,0);primaryCallbackMask=(bits>>1)&3;
                using var bounded=CancellationTokenSource.CreateLinkedTokenSource(stopping.Token);bounded.CancelAfter(TimeSpan.FromMinutes(2));
                primaryCallback=primary(bits,bounded.Token).AsTask();await primaryCallback.WaitAsync(bounded.Token).ConfigureAwait(false);primaryCallback=null;
            }
            catch(OperationCanceledException)when(stopping.IsCancellationRequested){throw;}
            catch(Exception error){fault(null,new NativeDeliveryFailure("Primary",primaryCallbackMask,error));}}
        catch(OperationCanceledException)when(stopping.IsCancellationRequested){}
    }

    /// <summary>Joins actual option callbacks and excludes concurrent readers
    /// while replacing native ownership. Futures delivery remains independent.
    /// A non-cooperative callback consumes the existing deadline and fails proof.</summary>
    public async Task ReplaceOptionOwnerAsync(Func<Task> replace,CancellationToken token)
    {
        await optionObservation.WaitAsync(token).ConfigureAwait(false);
        try{await quotes.ExclusiveAsync(()=>trades.ExclusiveAsync(replace,token),token).ConfigureAwait(false);ObserveOptionStatus();}
        finally{optionObservation.Release();}
        quotePulse.Writer.TryWrite(1);tradePulse.Writer.TryWrite(1);
    }

    /// <summary>Proves that prior option callbacks have physically joined without
    /// canceling the receiver. A blocked managed writer cannot be disguised as a
    /// successful transport reset. The Host supplies its original deadline token.</summary>
    public async Task QualifyOptionRecoveryAsync(CancellationToken token)
    {
        await quotes.JoinAsync(token).ConfigureAwait(false);
        await trades.JoinAsync(token).ConfigureAwait(false);
        foreach(var task in new[]{receiver,quoteWorker,tradeWorker})
            if(task.IsCompleted)throw new InvalidOperationException("Native option delivery loop has ended.",task.Exception);
    }

    /// <summary>Cancels all receivers before joining any. One original two-minute
    /// shutdown budget also joins callbacks that ignored cancellation. A timeout
    /// is reported to the owner; it never claims those callbacks were terminated.</summary>
    public ValueTask DisposeAsync()=>StopAndDisposeAsync(TimeSpan.FromMinutes(2));
    /// <summary>Joins using the epoch's remaining original deadline. The first
    /// call fixes the budget; a second call cannot renew a timed-out shutdown.
    /// Expiry retains callback ownership and does not claim physical termination.</summary>
    public ValueTask StopAndDisposeAsync(TimeSpan remainingBudget)
    {
        if(remainingBudget<TimeSpan.Zero)throw new ArgumentOutOfRangeException(nameof(remainingBudget));
        if(remainingBudget>TimeSpan.FromMinutes(2))remainingBudget=TimeSpan.FromMinutes(2);
        lock(disposalGate)return new(disposal??=StopAsync(remainingBudget));
    }
    async Task StopAsync(TimeSpan remainingBudget)
    {
        var started=Stopwatch.GetTimestamp();stopping.Cancel();
        using var deadline=new CancellationTokenSource(remainingBudget);
        Exception? failed=null;
        try{await Completion.WaitAsync(deadline.Token).ConfigureAwait(false);}
        catch(Exception error)when(Completion.IsCompleted){failed=error;}
        await quotes.JoinAsync(deadline.Token).ConfigureAwait(false);await trades.JoinAsync(deadline.Token).ConfigureAwait(false);
        if(primaryCallback is { } pending)try{await pending.WaitAsync(deadline.Token).ConfigureAwait(false);}catch when(pending.IsCompleted){_=pending.Exception;}
        Volatile.Write(ref joined,1);
        if(Stopwatch.GetElapsedTime(started)>=remainingBudget)throw new TimeoutException("Delivery callback shutdown deadline expired.");
        if(failed is not null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failed).Throw();
    }
}
