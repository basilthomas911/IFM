using TomasAI.IFM.Application.MarketData.OptionChainCache;
using TomasAI.IFM.Application.MarketData.Rust.Api;
namespace TomasAI.IFM.Application.MarketData.Rust.Host;
/// <summary>Coordinates application session retirement before provider teardown.
/// Durable trade/business intent is preserved for an API restart; market-close
/// workflows remove their dated intent through the existing public API.</summary>
public sealed class NativeHostLifecycle(IServiceProvider services,NativeHostState state):IRustMarketDataLifecycle
{
    /// <inheritdoc/>
    public async Task StartingAsync(DateOnly date,CancellationToken token)
    {await services.GetRequiredService<NativeHostApplicationServices>().PrepareSessionAsync(date,token).ConfigureAwait(false);}
    /// <inheritdoc/>
    public Task StartedAsync(DateOnly date,CancellationToken token){token.ThrowIfCancellationRequested();state.Capture(true);services.GetRequiredService<NativeBootstrapPrices>().StartSession(date);return Task.CompletedTask;}
    /// <inheritdoc/>
    public async Task StoppingAsync(DateOnly date,CancellationToken token)
    {
        services.GetRequiredService<NativeBootstrapPrices>().StopSession(date);
        state.CloseOptionsAdmission(date);var failures=new List<Exception>();
        try{await services.GetRequiredService<QualifiedIndividualOptionFeeds>().RetireSessionAsync(token).WaitAsync(token).ConfigureAwait(false);}catch(Exception error){failures.Add(error);}
        try{await services.GetRequiredService<IOptionFeedSessionCleanup>().CloseSessionAsync(token).WaitAsync(token).ConfigureAwait(false);}catch(Exception error){failures.Add(error);}
        if(failures.Count!=0)throw new AggregateException("Native application scope retirement failed.",failures);
    }
}
