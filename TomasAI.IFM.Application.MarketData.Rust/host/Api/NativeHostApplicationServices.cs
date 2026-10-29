using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Recovery;
using TomasAI.IFM.Application.MarketData.Rust.Api;
using TomasAI.IFM.Application.MarketData.Rust.Host.OptionChains;
using TomasAI.IFM.Application.MarketData.Subscriptions;
using TomasAI.IFM.Application.MarketData.TradeStrategySymbols;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Recovery;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.Contracts;
using TomasAI.IFM.Shared.MarketData.Contracts.Ticker;
namespace TomasAI.IFM.Application.MarketData.Rust.Host;

/// <summary>Application policy and persistence around native providers. This
/// service does not instantiate a legacy driver, worker supervisor or process.
/// Every business timestamp uses the same clock supplied to native epochs.</summary>
public sealed class NativeHostApplicationServices(IServiceProvider services,NativeMarketDataEpochFactory epochs,TimeProvider clock)
{
    NativeMarketDataEpochFactory Epochs=>epochs;
    T Get<T>() where T:notnull=>services.GetRequiredService<T>();
    IMarketDataApi Api=>Get<IMarketDataApi>();
    QualifiedIndividualOptionFeeds Feeds=>Get<QualifiedIndividualOptionFeeds>();
    readonly object recoveryGate=new();
    TradeFeedRecoveryService? recovery;
    TradeFeedRecoveryService Recovery
    {get{lock(recoveryGate)return recovery??=new(Get<ITradeFeedRecoveryStore>(),new RecoveryRuntime(this),services.GetService<ILogger<TradeFeedRecoveryService>>());}}
    /// <summary>Prepared snapshots backed exclusively by native market composition.</summary>
    public IOptionChainCache OptionChainCache=>Get<IOptionChainCache>();
    /// <summary>Requalifies every preserved option against the current reference
    /// authority for a newly joined native owner; does not copy expired contexts.</summary>
    public async Task<System.Collections.Immutable.ImmutableArray<WorkerOptionDefinition>> RequalifyOptionsAsync(
        System.Collections.Immutable.ImmutableArray<WorkerOptionDefinition> options,Guid generation,CancellationToken token)
    {
        var builder=System.Collections.Immutable.ImmutableArray.CreateBuilder<WorkerOptionDefinition>(options.Length);
        foreach(var option in options)
        {
            var prepared=await Get<IOptionPricingContextProvider>().PrepareAsync(option.Pricing.Contract,option.Pricing.Calendar,
                Get<TreasuryPublicationPolicy>(),Get<TreasuryRateConversionPolicy>(),generation,Black76PricingModel.EngineFor(option.Pricing.Contract),clock.GetUtcNow(),token).ConfigureAwait(false);
            if(prepared.Failure is{} failure)throw new InvalidOperationException($"Option recovery qualification failed: {failure.Code}: {failure.Detail}");
            builder.Add(option with{Pricing=prepared.Context! with{
                MaximumQuoteAgeMilliseconds=Math.Min(prepared.Context!.MaximumQuoteAgeMilliseconds,option.Pricing.MaximumQuoteAgeMilliseconds),
                MaximumQuoteSkewMilliseconds=Math.Min(prepared.Context.MaximumQuoteSkewMilliseconds,option.Pricing.MaximumQuoteSkewMilliseconds)}});
        }
        return builder.MoveToImmutable();
    }
    /// <summary>Updates existing application lease registries to the replaced
    /// native generation before saved trade intent is reconciled.</summary>
    public async Task AdoptRestoredOptionsAsync(IReadOnlyList<WorkerOptionChainRequest> leases,CancellationToken token,Action? activate=null)
    {
        var discovery=Get<QualifiedCompositionDiscovery>();
        // Explicit release wins even when its old-generation transport release
        // raced native replacement. Do not resurrect forgotten discovery owners.
        var wanted=leases.Where(x=>discovery.IsLeaseCurrent(x.LeaseId)).ToArray();
        foreach(var retired in leases.Except(wanted))
            await epochs.Current.ReleaseOptionChainAsync(new(retired.ScopeId,retired.LeaseId,retired.GenerationId),token).ConfigureAwait(false);
        discovery.AdoptRestoredLeases(wanted);
        await Feeds.AdoptRestoredLeasesAsync(wanted,token,activate).ConfigureAwait(false);
    }
    /// <summary>Loads the database's current contracts before creating a live epoch.</summary>
    public async Task<FuturesContractV3ReadModel[]> PrepareSessionAsync(DateOnly date,CancellationToken token=default)
    {
        await Get<IDatabentoContractAuthority>().ReconcileAsync(date,nameof(NativeHostApplicationServices),token).ConfigureAwait(false);
        return PreparedContracts();
    }
    /// <summary>Returns the already-qualified registry mapping without database I/O.</summary>
    public FuturesContractV3ReadModel[] PreparedContracts()
    {
        var registry=Get<IDatabentoContractRegistrationRegistry>();
        var result=new Dictionary<string,FuturesContractV3ReadModel>(StringComparer.Ordinal);
        foreach(var registration in registry.Snapshot())
        {
            if(registry.TryGetFuturesTermStructureContracts(registration.RootSymbol!,out var pair))
            {result[pair.Front.ContractId]=pair.Front;result[pair.Back.ContractId]=pair.Back;}
            else if(registry.TryGetOnTheRunFuturesContract(registration.RootSymbol!,out var contract))result[contract.ContractId]=contract;
        }
        return result.Values.ToArray();
    }
    /// <summary>Qualifies an owned individual option through current application policy.</summary>
    public async Task<bool> AcquireMonitoredOptionAsync(TickerStreamOwner owner,FuturesOptionContractReadModel contract,DateOnly date,CancellationToken token=default)
    {RequireDate(date);Feeds.ValidateOwner(owner);await Feeds.AcquireAsync(owner,contract,date,token).WaitAsync(token).ConfigureAwait(false);return true;}
    /// <summary>Releases monitoring authority without changing durable business intent.</summary>
    public async Task<bool> ReleaseMonitoredOptionAsync(TickerStreamOwner owner,string contract,CancellationToken token=default)
    {await Feeds.ReleaseAsync(owner,contract,token).WaitAsync(token).ConfigureAwait(false);return true;}
    void RequireDate(DateOnly date)
    {if(!epochs.TryGetCurrent(out var epoch)||epoch!.ValueDate!=date)throw new InvalidOperationException("Trade ValueDate differs from the active API epoch.");}
    /// <summary>Runs native reference I/O followed by unchanged snapshot persistence.</summary>
    public Task<InstrumentDefinitionSnapshot> RefreshInstrumentDefinitionsAsync(CancellationToken token=default)=>Get<InstrumentDefinitionRefresh>().RefreshAsync(token);
    /// <summary>Refreshes option calendar storage from native definitions.</summary>
    public Task<int> RefreshOptionCalendarAsync(string symbol,CancellationToken token=default)=>Get<OptionContractExpiryCalendarRefreshService>().RefreshAsync(symbol,token);
    /// <summary>Evaluates the same application chain policy over native snapshots.</summary>
    public Task<ServiceResult<EvaluatedOptionChainReadModel>> EvaluateOptionChainAsync(GetEvaluatedOptionChainQuery query,CancellationToken token=default)=>QualifiedEvaluatedOptionChain.ExecuteAsync(query,Get<HostOptionChainContext>(),token);
    /// <summary>Uses configured native treasury acquisition and unchanged publication policy.</summary>
    public Task<TreasuryPricingResult> WarmTreasuryPricingAsync(CancellationToken token=default)=>Get<TreasuryPricingProvider>().GetAsync(clock.GetUtcNow(),0,Get<TreasuryPublicationPolicy>(),Get<TreasuryRateConversionPolicy>(),token);
    /// <summary>Reads reference data without requiring a live ValueDate.</summary>
    public Task<TreasuryCurveSnapshot?> ReadTreasuryCurveAsync(DateOnly date,CancellationToken token=default)=>Get<ITreasuryCurve>().GetLatestAsync(date,token);
    /// <summary>Reads an inclusive range from the configured native reference source.</summary>
    public async Task<TreasuryCurveSnapshot[]> ReadTreasuryCurvesAsync(DateOnly from,DateOnly through,CancellationToken token=default)=>(await Get<ITreasuryCurve>().GetRangeAsync(from,through,token).ConfigureAwait(false)).ToArray();
    /// <summary>Reads provider calendar entries through native transport and parsing.</summary>
    public async Task<EconomicCalendarEntry[]> ReadEconomicCalendarAsync(DateOnly from,DateOnly through,string[]? countries,CancellationToken token=default)=>(await Get<IEconomicCalendar>().GetAsync(from,through,countries?.ToHashSet(StringComparer.OrdinalIgnoreCase),token).ConfigureAwait(false)).ToArray();
    /// <summary>Retains existing approval/cost policy around native history estimates.</summary>
    /// <summary>Reads historical provider availability through the application adapter.</summary>
    public Task<DateTimeOffset?> GetHistoricalAvailableEndAsync(MarketDataHistoricalRequest request,CancellationToken token=default)=>Get<IMarketDataHistoricalApi>().GetAvailableEndAsync(request,token).AsTask();
    public Task<MarketDataHistoricalEstimate> EstimateHistoricalAsync(MarketDataHistoricalRequest request,CancellationToken token=default)=>Get<IMarketDataHistoricalApi>().EstimateAsync(request,token).AsTask();
    /// <summary>Retains durable loader checkpoints and database commits around native history readers.</summary>
    public Task<HistoricalDataLoaderState> LoadHistoricalAsync(MarketDataHistoricalRequest request,CancellationToken token=default)=>Get<HistoricalDataLoader>().ExecuteAsync(request,token).AsTask();
    /// <summary>Acquires application-qualified references before a native scope starts.</summary>
    public Task<CompositionDiscoveryResult> AcquireOptionScopeAsync(CompositionDiscoveryRequest request,CancellationToken token=default)=>Get<QualifiedCompositionDiscovery>().AcquireAsync(request,token,renewAutomatically:true);
    /// <summary>Releases a temporary scope lease through its native logical owner.</summary>
    public Task<WorkerOptionChainResult> ReleaseOptionScopeAsync(WorkerOptionChainRequest request,CancellationToken token=default)=>Get<QualifiedCompositionDiscovery>().ReleaseAsync(request,token);
    /// <summary>Captures native option and futures data with exact source identities.</summary>
    public Task<CompositionSnapshotResult> CaptureCompositionAsync(string dataset,CompositionSnapshotRequest request,CancellationToken token=default)=>Get<ICompositionMarketDataApi>().CaptureAsync(dataset,request,token);
    /// <summary>Reads individually qualified option risk from native snapshots.</summary>
    public Task<MarketCompositionSnapshot?> CaptureOptionRiskAsync(string contract,DateOnly date,CancellationToken token=default)=>Get<IndividualOptionRiskReader>().CaptureAsync(contract,date,token);
    /// <summary>Releases committed scope ownership through native revision checks.</summary>
    public Task<WorkerOptionChainResult> ReleaseCompositionAsync(string dataset,WorkerOptionChainRelease request,CancellationToken token=default)=>Get<ICompositionMarketDataApi>().ReleaseAsync(dataset,request,token);
    /// <summary>Prepares and persists the existing application composition from native sources.</summary>
    public Task<CompositionPreparationResult> PrepareCompositionAsync(CompositionPreparationKey key,CompositionMarketDataPlan plan,string horizon,DateTimeOffset deadline,CancellationToken token=default)=>Get<CompositionMarketPreparation>().PrepareAsync(key,plan,horizon,deadline,token);
    /// <summary>Uses the same prepared snapshot cache as the public facade.</summary>
    public Task<CompositionPreparationResult> PrepareCachedCompositionAsync(CompositionPreparationKey key,OptionChainSnapshotRequest request,CancellationToken token=default)=>Get<CompositionMarketPreparation>().PrepareCachedAsync(key,OptionChainCache,request,token);
    /// <summary>Reconciles committed intent through native ownership realization.</summary>
    public Task<DurableRealization?> ReconcileCommittedSubscriptionsAsync(CancellationToken token=default)=>Get<DurableCompositionRuntime>().ReconcileOnceAsync(token);
    /// <summary>Builds the existing indicator seed through native replay/history.</summary>
    public Task<TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal.FuturesTradeSessionBarReadModel[]> ReadFiveMinuteSeedBarsAsync(string contractId,CancellationToken token=default)=>Get<Seeding.NativeFuturesSeedReplay>().ReadAsync(contractId,token);
    /// <summary>Loads saved intent for the API-supplied trading date.</summary>
    public Task<bool> LoadTradeFeedRecoveryAsync(string trade,DateOnly date,CancellationToken token=default)=>Recovery.LoadAsync(trade,date,false,token);
    /// <summary>Enables and persists a trade's option feed intent.</summary>
    public Task<bool> EnableTradeFeedRecoveryAsync(string trade,DateOnly date,CancellationToken token=default)=>Recovery.LoadAsync(trade,date,true,token);
    /// <summary>Disables and removes saved live-feed intent.</summary>
    public async Task<bool> DisableTradeFeedRecoveryAsync(string trade,DateOnly date,CancellationToken token=default){await Recovery.DisableAsync(trade,date,token).ConfigureAwait(false);return true;}
    /// <summary>Disposes the trading day's recovery intent at market close.</summary>
    public async Task<bool> CloseTradeFeedRecoveryAsync(DateOnly date,CancellationToken token=default){await Recovery.CloseAsync(date,token).ConfigureAwait(false);return true;}
    /// <summary>Returns the durable recovery registrations for one explicit date.</summary>
    public Task<TradeFeedRecoveryRegistration[]> ReadTradeFeedRecoveryAsync(DateOnly date,CancellationToken token=default)=>Recovery.ReadAsync(date,token);
    /// <summary>Returns enabled trade identities for backend health and API restoration.</summary>
    public string[] EnabledTrades(DateOnly date)=>Recovery.EnabledTrades(date);
    sealed class RecoveryRuntime(NativeHostApplicationServices owner):ITradeFeedRecoveryRuntime
    {
        public DateOnly? ActiveDate=>owner.Epochs.TryGetCurrent(out var epoch)?epoch!.ValueDate:null;
        public Task<EstablishedTradeDefinition?> LoadAsync(TradeEntityId trade,CancellationToken token)=>owner.Get<IDbContextFactory>().TradeDb.GetEstablishedTradeAsync(trade,token);
        TickerStreamOwner Lease(string id,string contract)=>new("TradeFeedRecovery",id+"."+owner.Feeds.OwnershipGeneration.ToString("N"),contract);
        public async Task StartAsync(string id,string contract,DateOnly date,CancellationToken token)
        {
            var definition=await owner.Get<IDbContextFactory>().SecuritiesDb.GetFuturesOptionContractAsync(contract,token).ConfigureAwait(false)??throw new InvalidOperationException("Recovery option contract not found: "+contract);
            await owner.AcquireMonitoredOptionAsync(Lease(id,contract),definition,date,token).ConfigureAwait(false);
        }
        public async Task StopAsync(string id,string contract,CancellationToken token)=>await owner.ReleaseMonitoredOptionAsync(Lease(id,contract),contract,token).ConfigureAwait(false);
    }
}
