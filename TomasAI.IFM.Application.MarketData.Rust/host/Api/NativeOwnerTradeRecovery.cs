using TomasAI.IFM.Application.MarketData.Recovery;
using TomasAI.IFM.Application.MarketData.Rust.Api;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Recovery;
using TomasAI.IFM.Shared.MarketData.Contracts.Ticker;
namespace TomasAI.IFM.Application.MarketData.Rust.Host;

/// <summary>Persistence of copied trade business intent. It retains no native
/// lease, physical source, pricing context or recovery authority. Starting a
/// saved contract accepts canonical demand before any metadata lookup.</summary>
public sealed class NativeOwnerTradeRecovery
{
    readonly TradeFeedRecoveryService recovery;
    readonly NativeDatasetBackend backend;
    readonly IDbContextFactory databases;
    /// <summary>Composes existing business persistence around native commands.</summary>
    public NativeOwnerTradeRecovery(NativeDatasetBackend backend,ITradeFeedRecoveryStore store,
        IDbContextFactory databases,ILogger<TradeFeedRecoveryService> logger)
    {this.backend=backend;this.databases=databases;recovery=new(store,new Runtime(this),logger);}
    NativeOwnedOptionFeeds Feeds=>new(backend.Runtime,backend.OptionGateway,backend.Broker,backend.Policy,backend.Scope);
    /// <summary>Loads saved intent or explicitly enables it for the API date.</summary>
    public async Task<bool> LoadAsync(string trade,DateOnly date,bool enable,CancellationToken token)
    {
        // Admit every saved leg before observing native readiness. Awaiting the
        // first leg here used to prevent the remaining legs reaching Rust.
        var enabled=await recovery.LoadAsync(trade,date,enable,token).ConfigureAwait(false);
        if(enabled)await backend.Runtime.WaitDatasetOwnerReadyAsync(Delivery.MarketDataLane.OptionsGlbx,true,token).ConfigureAwait(false);
        return enabled;
    }
    /// <summary>Explicitly disables and removes saved business intent.</summary>
    public async Task<bool> DisableAsync(string trade,DateOnly date,CancellationToken token){await recovery.DisableAsync(trade,date,token).ConfigureAwait(false);return true;}
    /// <summary>Disposes saved intent at market close.</summary>
    public async Task<bool> CloseAsync(DateOnly date,CancellationToken token){await recovery.CloseAsync(date,token).ConfigureAwait(false);return true;}
    /// <summary>Reads copied persisted intent without creating any feed resource.</summary>
    public Task<TradeFeedRecoveryRegistration[]> ReadAsync(DateOnly date,CancellationToken token)=>recovery.ReadAsync(date,token);
    /// <summary>Copies enabled business identifiers for recovery verification.</summary>
    public string[] EnabledTrades(DateOnly date)=>recovery.EnabledTrades(date);
    sealed class Runtime(NativeOwnerTradeRecovery owner):ITradeFeedRecoveryRuntime
    {
        public DateOnly? ActiveDate=>owner.backend.Runtime.ReadDatasetOwnerBackendStatus(true).ValueDate;
        public Task<EstablishedTradeDefinition?> LoadAsync(TradeEntityId trade,CancellationToken token)=>owner.databases.TradeDb.GetEstablishedTradeAsync(trade,token);
        static TickerStreamOwner Identity(string trade,string contract)=>new("TradeFeedRecovery",trade,contract);
        public Task StartAsync(string trade,string contract,DateOnly date,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();owner.Feeds.AcceptCanonical(Identity(trade,contract),contract,date,date);
            // This is command acceptance, not a claim of feed readiness. The
            // public operation observes readiness once after the complete batch.
            return Task.CompletedTask;
        }
        public async Task StopAsync(string trade,string contract,CancellationToken token)=>await owner.Feeds.ReleaseAsync(Identity(trade,contract),contract,token).ConfigureAwait(false);
    }
}
