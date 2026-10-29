using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Framework.MarketData.ReferenceData;
namespace TomasAI.IFM.Application.MarketData.Rust.Host;
/// <summary>Host-owned reference/history runtime, independent of live ValueDate.
/// Provider requests remain lazy. Its event-driven warning receiver drains every
/// native source notice without polling or starting a market-data session.</summary>
public sealed class NativeColdBackend:IAsyncDisposable
{
    readonly CancellationTokenSource stopping=new();readonly Task notices;
    readonly ILogger<NativeColdBackend> logger;
    readonly object gate=new();Task? disposal;
    /// <summary>True while the owned notice/completion delivery is healthy.</summary>
    public bool IsHealthy=>disposal is null&&!notices.IsCompleted;
    /// <summary>Native cold operation owner.</summary>
    public NativeMarketDataRuntime Runtime{get;}
    /// <summary>Configured native library path; never an alternate legacy DLL.</summary>
    public string LibraryPath{get;}
    /// <summary>Explicit native resolver endpoints, copied from configuration.</summary>
    public string[] Dns{get;}
    /// <summary>Historical source URL, preserving the existing provider origin.</summary>
    public string HistoryEndpoint{get;}
    readonly string? key;bool historyConfigured;
    /// <summary>Loads native configuration only, with no network activity.</summary>
    public NativeColdBackend(IConfiguration configuration,TimeProvider clock,ILogger<NativeColdBackend> logger)
    {
        this.logger=logger;
        LibraryPath=Path.GetFullPath(configuration["MarketDataHost:Rust:LibraryPath"]??Path.Combine(AppContext.BaseDirectory,"ifm_market_data_native.dll"));
        Dns=configuration.GetSection("MarketDataHost:Rust:DnsServers").Get<string[]>()??["1.1.1.1:53","8.8.8.8:53"];
        HistoryEndpoint=configuration["MarketDataHost:Rust:HistoryEndpoint"]??"https://hist.databento.com/";
        key=Environment.GetEnvironmentVariable("DATABENTO_API_KEY");
        Runtime=new(LibraryPath,configuration.GetValue("MarketDataHost:Rust:Threads",2u));
        try
        {
            Runtime.ConfigureApiClock(clock.GetUtcNow(),configuration.GetValue("MarketDataHost:Rust:AdvancingClock",true));

            Runtime.ConfigureTreasury(configuration["MarketDataHost:Rust:TreasuryEndpoint"]??UsTreasuryCurve.Endpoint,Dns);
            notices=ObserveAsync();
        }
        catch
        {
            // Construction has not submitted network/decoder tasks. The root
            // still owns its native completion pump and must be explicitly joined.
            Runtime.StopAndDisposeAsync(TimeSpan.FromMinutes(2)).AsTask().GetAwaiter().GetResult();throw;
        }
    }
    internal string ProviderKey=>key??throw new InvalidOperationException("DATABENTO_API_KEY is required by native Databento operations.");
    internal NativeMarketDataRuntime HistoricalRuntime
    {
        get{lock(gate){if(!historyConfigured){Runtime.ConfigureHistory(HistoryEndpoint,ProviderKey,Dns);historyConfigured=true;}return Runtime;}}
    }
    async Task ObserveAsync()
    {
        try
        {
            while(true)
            {
                var bits=await Runtime.WaitForLiveChangesAsync(stopping.Token).ConfigureAwait(false);
                if((bits&16)==0)continue;
                HistoricalWarning[] batch;
                do{batch=Runtime.DrainHistoricalWarnings();foreach(var notice in batch)logger.Log(notice.Endpoint=="live.replay.system"&&notice.Status is 1 or 3?LogLevel.Information:LogLevel.Warning,"Native provider notice; Endpoint={Endpoint}; Status={Status}; RequestId={RequestId}; Message={Message}",notice.Endpoint,notice.Status,notice.RequestId,notice.Text);}while(batch.Length==16);
            }
        }
        catch(OperationCanceledException)when(stopping.IsCancellationRequested){}
        catch(Exception error){logger.LogCritical(error,"Native reference/history notice receiver failed");throw;}
    }
    /// <inheritdoc/>
    public ValueTask DisposeAsync()=>StopAndDisposeAsync(TimeSpan.FromMinutes(2));
    /// <summary>Fixes shutdown to the Host's original remaining budget.</summary>
    public ValueTask StopAndDisposeAsync(TimeSpan budget)
    {if(budget<TimeSpan.Zero)throw new ArgumentOutOfRangeException(nameof(budget));if(budget>TimeSpan.FromMinutes(2))budget=TimeSpan.FromMinutes(2);lock(gate)return new(disposal??=DisposeCoreAsync(budget));}
    async Task DisposeCoreAsync(TimeSpan budget)
    {
        var started=System.Diagnostics.Stopwatch.GetTimestamp();
        TimeSpan Remaining()=>TimeSpan.FromTicks(Math.Max(0,budget.Ticks-System.Diagnostics.Stopwatch.GetElapsedTime(started).Ticks));
        stopping.Cancel();var failures=new List<Exception>();
        try{await notices.WaitAsync(Remaining()).ConfigureAwait(false);}catch(Exception error){failures.Add(error);}
        // Always signal/join the native root, even after a logging observer fails
        // to join. Cleanup cannot renew the original shutdown deadline.
        try{await Runtime.StopAndDisposeAsync(Remaining()).ConfigureAwait(false);}catch(Exception error){failures.Add(error);}
        if(notices.IsCompleted)stopping.Dispose();
        else _=notices.ContinueWith(task=>_=task.Exception,CancellationToken.None,TaskContinuationOptions.OnlyOnFaulted,TaskScheduler.Default);
        if(failures.Count!=0)throw new AggregateException("Native cold backend shutdown failed.",failures);
    }
}
