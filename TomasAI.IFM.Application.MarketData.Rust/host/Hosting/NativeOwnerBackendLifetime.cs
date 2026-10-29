using System.Diagnostics;
namespace TomasAI.IFM.Application.MarketData.Rust.Host;
/// <summary>Qualifies the complete native root on Host startup and joins both
/// live/cold native roots on shutdown. No managed epoch or feed registry exists.</summary>
public sealed class NativeOwnerBackendLifetime(NativeDatasetBackend backend,NativeColdBackend cold,
    ILogger<NativeOwnerBackendLifetime> logger):IHostedService
{
    /// <inheritdoc/>
    public Task StartAsync(CancellationToken token)
    {token.ThrowIfCancellationRequested();logger.LogInformation("Native dataset-owner Host ready; ProcessId={Process}; live sessions start through IMarketDataApi",Environment.ProcessId);return Task.CompletedTask;}
    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken token)
    {
        var started=Stopwatch.GetTimestamp();TimeSpan Remaining()=>TimeSpan.FromTicks(Math.Max(0,TimeSpan.FromMinutes(2).Ticks-Stopwatch.GetElapsedTime(started).Ticks));
        var errors=new List<Exception>();
        // Cancel native live control before any cold query/database/broker wait.
        try{await backend.StopAndDisposeAsync(Remaining()).ConfigureAwait(false);}catch(Exception error){errors.Add(error);}
        try{await cold.StopAndDisposeAsync(Remaining()).ConfigureAwait(false);}catch(Exception error){errors.Add(error);}
        if(errors.Count!=0)throw new AggregateException("Native Host shutdown failed within its original deadline.",errors);
    }
}
