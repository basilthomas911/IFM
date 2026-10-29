namespace TomasAI.IFM.Application.MarketData.Rust.Api;
/// <summary>Host application lifecycle hooks around the unchanged public API.
/// Standalone epoch qualification can omit Host policy; production Host composition
/// registers these hooks to retire application scopes before native shutdown.</summary>
public interface IRustMarketDataLifecycle
{
    /// <summary>Reconciles authoritative stored contract roles before native bootstrap.</summary>
    Task StartingAsync(DateOnly date,CancellationToken token);
    /// <summary>Admits application metadata after native bootstrap succeeds.</summary>
    Task StartedAsync(DateOnly date,CancellationToken token);
    /// <summary>Retires application scopes before native owners are disposed.</summary>
    Task StoppingAsync(DateOnly date,CancellationToken token);
}
