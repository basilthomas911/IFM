using System.Diagnostics;
using TomasAI.IFM.Application.MarketData.Contracts;
namespace TomasAI.IFM.Application.MarketData.Rust.Host;
/// <summary>Loads/qualifies the DLL on Host startup and joins both native owners
/// on shutdown. Starting the Host alone never subscribes to a market dataset.</summary>
public sealed class NativeBackendLifetime(IServiceProvider services,NativeHostState state,ILogger<NativeBackendLifetime> logger):IHostedService
{
    NativeColdBackend? cold;
    /// <inheritdoc/>
    public Task StartAsync(CancellationToken token)
    {token.ThrowIfCancellationRequested();cold=services.GetRequiredService<NativeColdBackend>();logger.LogInformation("Native market-data Host ready; ProcessId={Process}; Provider sessions start only through IMarketDataApi",Environment.ProcessId);return Task.CompletedTask;}
    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken token)
    {
        var started=Stopwatch.GetTimestamp();TimeSpan Remaining()=>TimeSpan.FromTicks(Math.Max(0,TimeSpan.FromMinutes(2).Ticks-Stopwatch.GetElapsedTime(started).Ticks));
        var failures=new List<Exception>();
        if(state.ValueDate is{} date)
            try{await services.GetRequiredService<IMarketDataApi>().StopAsync(date).WaitAsync(Remaining(),token).ConfigureAwait(false);}catch(Exception error){failures.Add(error);}
        if(cold is not null)try{await cold.StopAndDisposeAsync(Remaining()).ConfigureAwait(false);}catch(Exception error){failures.Add(error);}
        if(failures.Count!=0)throw new AggregateException("Native Host shutdown failed within its original deadline.",failures);
    }
}
