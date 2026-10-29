using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.TradeStrategySymbols;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Rust.Api;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Recovery;
using TomasAI.IFM.Shared.MarketData.Contracts;
using TomasAI.IFM.Shared.MarketData.Contracts.Ticker;
namespace TomasAI.IFM.Application.MarketData.Rust.Host;

/// <summary>Business persistence and copied reference operations around the
/// complete native owner. Live commands/captures execute in Rust without C#
/// reference preparation, scope registries, renewal loops or admission gates.</summary>
public sealed partial class NativeOwnerBusinessOperations(IServiceProvider services,
    NativeDatasetBackend backend,NativeOwnerTradeRecovery trades,TimeProvider clock)
{
    T Get<T>()where T:notnull=>services.GetRequiredService<T>();
    NativeOwnedOptionFeeds Feeds=>new(backend.Runtime,backend.OptionGateway,backend.Broker,backend.Policy,backend.Scope);
    void RequireDate(DateOnly date){if(backend.Runtime.ReadDatasetOwnerBackendStatus(true).ValueDate!=date)throw new InvalidOperationException("Trade ValueDate differs from the active native API session.");}
    /// <summary>Query-only native prepared snapshot facade.</summary>
    public IOptionChainCache OptionChainCache=>Get<NativeOwnedOptionChainCache>();
    /// <summary>Copies raw contracts; Rust prepares references and source resources.</summary>
    public Task<bool> AcquireMonitoredOptionAsync(TickerStreamOwner owner,FuturesOptionContractReadModel contract,DateOnly date,CancellationToken token=default)
    {RequireDate(date);return Feeds.AcquireAsync(owner,contract,date,date,token);}
    /// <summary>Explicitly withdraws native business ownership and observes join.</summary>
    public Task<bool> ReleaseMonitoredOptionAsync(TickerStreamOwner owner,string contract,CancellationToken token=default)=>Feeds.ReleaseAsync(owner,contract,token);
    /// <summary>Captures coherent source/pricing evidence exclusively in Rust.</summary>
    public Task<CompositionSnapshotResult> CaptureCompositionAsync(string dataset,CompositionSnapshotRequest request,CancellationToken token=default)=>backend.Runtime.CaptureDatasetOwnerCompositionAsync(dataset,request,token);
    /// <summary>Reads current native-monitored valuation without local scope state.</summary>
    public Task<MarketCompositionSnapshot?> CaptureOptionRiskAsync(string contract,DateOnly date,CancellationToken token=default)=>backend.Runtime.CaptureDatasetOwnerOptionRiskAsync(contract,date,token);
    /// <summary>Reconciles copied contract catalog facts for startup/UI consumers.
    /// This cold storage operation is not a prerequisite for native cancellation.</summary>
    public async Task<FuturesContractV3ReadModel[]> PrepareSessionAsync(DateOnly date,CancellationToken token=default)
    {await Get<IDatabentoContractAuthority>().ReconcileAsync(date,nameof(NativeOwnerBusinessOperations),token).ConfigureAwait(false);return PreparedContracts();}
    /// <summary>Returns detached current registry contracts without acquisition.</summary>
    public FuturesContractV3ReadModel[] PreparedContracts()
    {
        var registry=Get<IDatabentoContractRegistrationRegistry>();var result=new Dictionary<string,FuturesContractV3ReadModel>(StringComparer.Ordinal);
        foreach(var registration in registry.Snapshot()){
            if(registry.TryGetFuturesTermStructureContracts(registration.RootSymbol!,out var pair)){result[pair.Front.ContractId]=pair.Front;result[pair.Back.ContractId]=pair.Back;}
            else if(registry.TryGetOnTheRunFuturesContract(registration.RootSymbol!,out var contract))result[contract.ContractId]=contract;
        }
        return result.Values.ToArray();
    }
    /// <summary>Native cold definition acquisition followed by storage publication.</summary>
    public Task<InstrumentDefinitionSnapshot> RefreshInstrumentDefinitionsAsync(CancellationToken token=default)=>Get<InstrumentDefinitionRefresh>().RefreshAsync(token);
    /// <summary>Persists copied native expiry definitions independently of live datasets.</summary>
    public Task<int> RefreshOptionCalendarAsync(string symbol,CancellationToken token=default)=>Get<OptionChains.OptionContractExpiryCalendarRefreshService>().RefreshAsync(symbol,token);
    /// <summary>Preserves the public cold Treasury admission policy around
    /// native retrieval/conversion. Live dataset context preparation never uses it.</summary>
    public Task<TreasuryPricingResult> WarmTreasuryPricingAsync(CancellationToken token=default)=>Get<TreasuryPricingProvider>().GetAsync(clock.GetUtcNow(),0,Get<TreasuryPublicationPolicy>(),Get<TreasuryRateConversionPolicy>(),token);
    /// <summary>Reads reference data from the independent native cold runtime.</summary>
    public Task<TreasuryCurveSnapshot?> ReadTreasuryCurveAsync(DateOnly date,CancellationToken token=default)=>Get<ITreasuryCurve>().GetLatestAsync(date,token);
    /// <summary>Reads an inclusive native Treasury range.</summary>
    public async Task<TreasuryCurveSnapshot[]> ReadTreasuryCurvesAsync(DateOnly first,DateOnly last,CancellationToken token=default)=>(await Get<ITreasuryCurve>().GetRangeAsync(first,last,token).ConfigureAwait(false)).ToArray();
    /// <summary>Reads native-acquired provider calendar facts.</summary>
    public async Task<EconomicCalendarEntry[]> ReadEconomicCalendarAsync(DateOnly first,DateOnly last,string[]? countries,CancellationToken token=default)=>(await Get<IEconomicCalendar>().GetAsync(first,last,countries?.ToHashSet(StringComparer.OrdinalIgnoreCase),token).ConfigureAwait(false)).ToArray();
    /// <summary>Retains business approval/cost policy around native history estimates.</summary>
    /// <summary>Reads historical provider availability through the application adapter.</summary>
    public Task<DateTimeOffset?> GetHistoricalAvailableEndAsync(MarketDataHistoricalRequest request,CancellationToken token=default)=>Get<IMarketDataHistoricalApi>().GetAvailableEndAsync(request,token).AsTask();
    public Task<MarketDataHistoricalEstimate> EstimateHistoricalAsync(MarketDataHistoricalRequest request,CancellationToken token=default)=>Get<IMarketDataHistoricalApi>().EstimateAsync(request,token).AsTask();
    /// <summary>Persists native historical data with existing durable checkpoints.</summary>
    public Task<HistoricalDataLoaderState> LoadHistoricalAsync(MarketDataHistoricalRequest request,CancellationToken token=default)=>Get<HistoricalDataLoader>().ExecuteAsync(request,token).AsTask();
    /// <summary>Builds indicator seed data using the independent native replay/history runtime.</summary>
    public Task<TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal.FuturesTradeSessionBarReadModel[]> ReadFiveMinuteSeedBarsAsync(string contract,CancellationToken token=default)=>Get<Seeding.NativeFuturesSeedReplay>().ReadAsync(contract,token);
    /// <summary>Loads saved business demand; native code resolves its contracts.</summary>
    public Task<bool> LoadTradeFeedRecoveryAsync(string trade,DateOnly date,CancellationToken token=default)=>trades.LoadAsync(trade,date,false,token);
    /// <summary>Enables persisted business demand through native commands.</summary>
    public Task<bool> EnableTradeFeedRecoveryAsync(string trade,DateOnly date,CancellationToken token=default)=>trades.LoadAsync(trade,date,true,token);
    /// <summary>Disables and removes saved demand.</summary>
    public Task<bool> DisableTradeFeedRecoveryAsync(string trade,DateOnly date,CancellationToken token=default)=>trades.DisableAsync(trade,date,token);
    /// <summary>Disposes saved demand at market close.</summary>
    public Task<bool> CloseTradeFeedRecoveryAsync(DateOnly date,CancellationToken token=default)=>trades.CloseAsync(date,token);
    /// <summary>Reads durable copied intent for the explicit API ValueDate.</summary>
    public Task<TradeFeedRecoveryRegistration[]> ReadTradeFeedRecoveryAsync(DateOnly date,CancellationToken token=default)=>trades.ReadAsync(date,token);
}
