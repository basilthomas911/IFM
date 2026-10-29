using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Rust.Api;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.Contracts.LastPrice;
using TomasAI.IFM.Shared.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Application.MarketData.Rust.Host;

/// <summary>Concrete command/query adapter for all application operations.
/// Live ownership, reference preparation, pricing, aggregation, publication and
/// recovery remain in Rust. Cold storage/catalog helpers retain copied facts;
/// they have no reference to a live dataset resource or managed provider epoch.</summary>
public sealed partial class NativeOwnerApplicationServices(NativeDatasetBackend backend,NativeColdBackend cold,
    IDatabentoContractRegistrationRegistry registrations,DatabentoMarketDataRuntimeOptions options,
    DatabentoMarketDataApiOptions apiOptions,ITradeStrategySymbolCatalog symbols,
    NativeColdRolloverOperations rollover,TimeProvider clock)
    : IRustMarketDataApplicationServices
{
    NativeMarketDataRuntime Runtime=>backend.Runtime;
    NativeOwnedOptionFeeds Feeds=>new(Runtime,backend.OptionGateway,backend.Broker,backend.Policy,backend.Scope);
    DateOnly Date=>Runtime.ReadDatasetOwnerBackendStatus(true).ValueDate??throw new MarketDataApiNotRunningException();
    TickerStreamOwner Owner(TickerStreamOwner? owner,string leg)=>owner??new(nameof(DatabentoMarketDataApi),$"compatibility:{Date:yyyy-MM-dd}",leg);

    /// <inheritdoc/>
    public Task StartAsync(DateOnly valueDate,Func<Guid,int,string,Task>? errorMessageHandler=null,CancellationToken cancellationToken=default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(valueDate,default);
        // PrepareSession is a separate copied-business-facts API operation.
        // Starting/observing a native dataset never waits for that database work.
        return backend.StartAsync(valueDate,backend.Demands(cold),cancellationToken);
    }
    /// <inheritdoc/>
    public Task StopAsync(DateOnly valueDate)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(valueDate,default);
        var active=Runtime.ReadDatasetOwnerBackendStatus(true).ValueDate;
        if(active is{} date&&date!=valueDate)throw new MarketDataApiValueDateMismatchException(date,valueDate);
        return backend.StopAsync();
    }
    /// <inheritdoc/>
    public ValueTask DisposeAsync()=>new(backend.StopAsync());
    /// <inheritdoc/>
    public MarketDataFeedRuntimeStatusReadModel GetRuntimeStatus()
    {
        var status=Runtime.ReadDatasetOwnerBackendStatus(true);
        return new(){IsRunning=status.ValueDate.HasValue,ActiveValueDate=status.ValueDate,ObservedAtUtc=clock.GetUtcNow()};
    }
    /// <inheritdoc/>
    public bool IsDatabentoFeedUp(TimeSpan? timeout=null)
    {
        if(timeout is{} wait&&wait<=TimeSpan.Zero)return false;
        var status=Runtime.ReadDatasetOwnerBackendStatus(true);var desired=status.Datasets.Where(row=>row.Lifecycle!="Idle").ToArray();
        return status.ValueDate.HasValue&&desired.Length>0&&desired.All(row=>row.Running&&row.Healthy);
    }
    /// <inheritdoc/>
    public bool IsTickDataStreamActive(string contractId)=>Runtime.IsDatasetOwnerStreamActive(contractId);
    /// <inheritdoc/>
    public FuturesMarketHealthSnapshot GetFuturesMarketHealth(string contractId)
    {
        var status=Runtime.ReadDatasetOwnerBackendStatus(true);var binding=Runtime.ReadDatasetOwnerBinding(contractId,false);
        var identity=binding is not null?Runtime.ReadDatasetOwnerIdentity(binding.Lane):null;
        var coherent=identity is not null&&binding is not null&&identity.Worker==binding.Worker&&identity.Generation==binding.Generation;
        var values=binding is not null?Runtime.ReadDatasetOwnerPrices(binding.Lane,contractId,binding.ValueDate):null;
        return new(status.ValueDate.HasValue,coherent&&binding!.Active&&binding.Healthy,
            coherent?$"{status.Datasets.Single(row=>row.Role=="Futures"&&(binding!.Lane==MarketDataLane.FuturesXcbf?row.Dataset=="XCBF.PITCH":row.Dataset=="GLBX.MDP3")).Dataset}|{identity!.Worker:N}|{identity.Generation:N}|{identity.Revision}":string.Empty,
            status.ValueDate,clock.GetUtcNow(),Math.Max(values?.Trade?.Sequence??0,values?.Quote?.Sequence??0));
    }
    NativeOwnerContractBinding Binding(string contract,bool option)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contract);_ = Date;
        return Runtime.ReadDatasetOwnerBinding(contract,option)??throw new MarketDataContractNotFoundException(contract);
    }
    /// <inheritdoc/>
    public IFuturesLastPriceReader GetFuturesLastPriceReader(string contract)
    {var binding=Binding(contract,false);return new NativeOwnedFuturesPriceReader(Runtime,binding.Lane,contract,binding.ValueDate);}
    /// <inheritdoc/>
    public IFuturesOptionLastPriceReader GetFuturesOptionLastPriceReader(string contract)
    {var binding=Binding(contract,true);return new NativeOwnedOptionPriceReader(Runtime,contract,binding.ValueDate);}
    Task<decimal?> Price(string contract,bool option)
    {
        _=Date;var result=Runtime.ReadDatasetOwnerPrice(contract,option,apiOptions.MaximumLastPriceAge);
        if(!result.Mapped)throw new MarketDataContractNotFoundException(contract);
        if(result.Failure=="InvalidFuturesOptionQuote")throw new InvalidFuturesOptionQuoteException(contract,"the bid exceeds the ask");
        if(result.Failure is not null)throw new InvalidDataException(result.Failure);
        return Task.FromResult(result.Price);
    }
    /// <inheritdoc/>
    public Task<decimal?> GetFuturesPriceAsync(string contract)=>Price(contract,false);
    /// <inheritdoc/>
    public Task<decimal?> GetFuturesOptionPriceAsync(string contract)=>Price(contract,true);
    /// <inheritdoc/>
    public bool TryGetMarketInstrumentId(string contract,out uint instrument)
    {
        var binding=Runtime.ReadDatasetOwnerBinding(contract,false)??Runtime.ReadDatasetOwnerBinding(contract,true);
        instrument=binding?.Instrument??0;return binding is not null;
    }
    /// <inheritdoc/>
    public bool TryGetLastTickPrice(string contract,out FuturesMarketPriceSnapshot snapshot)
    {var value=Runtime.ReadDatasetOwnerMarketPrice(contract);snapshot=value??default;return value.HasValue;}
    /// <inheritdoc/>
    public bool TryGetFuturesSessionStatistics(string contract,out FuturesSessionStatisticsSnapshot snapshot)
    {var value=Runtime.ReadDatasetOwnerStatistics(contract);snapshot=value??default;return value.HasValue;}
    /// <inheritdoc/>
    public bool TryGetLastOptionTickPrice(string contract,out OptionTickerPriceSnapshot snapshot)
    {var value=Runtime.ReadDatasetOwnerOptionTicker(contract);snapshot=value??default;return value.HasValue;}
    /// <inheritdoc/>
    public Task<bool> StartStreamingFuturesTickDataAsync(string contract,TickerStreamOwner? owner=null)
    {_=Binding(contract,false);return Task.FromResult(Runtime.ChangeDatasetOwnerRoute(Owner(owner,"futures"),contract,false,true));}
    /// <inheritdoc/>
    public Task<bool> StopStreamingFuturesTickDataAsync(string contract,TickerStreamOwner? owner=null)
        =>Task.FromResult(Runtime.ChangeDatasetOwnerRoute(Owner(owner,"futures"),contract,false,false));
    /// <inheritdoc/>
    public async Task<bool> StartStreamingFuturesOptionTickDataAsync(string contract,TickerStreamOwner? owner=null)
    {
        var date=Date;var identity=Owner(owner,"futures-option");
        Feeds.AcceptCanonical(identity,contract,date,date);
        var changed=Runtime.ChangeDatasetOwnerRoute(identity,contract,true,true);
        await Runtime.WaitDatasetOwnerReadyAsync(MarketDataLane.OptionsGlbx,true).ConfigureAwait(false);return changed;
    }
    /// <inheritdoc/>
    public async Task<bool> StopStreamingFuturesOptionTickDataAsync(string contract,TickerStreamOwner? owner=null)
    {
        var identity=Owner(owner,"futures-option");var changed=Runtime.ChangeDatasetOwnerRoute(identity,contract,true,false);
        await Feeds.ReleaseAsync(identity,contract).ConfigureAwait(false);return changed;
    }
    /// <inheritdoc/>
    public Task<ServiceResult<TradeStrategySymbolReadModel[]>> GetTradeStrategySymbolsAsync(TradeStrategyFamilyType family,CancellationToken token=default)=>symbols.GetAsync(family,token);
    /// <inheritdoc/>
    public bool TryGetOnTheRunFuturesContract(string symbol,out FuturesContractV3ReadModel contract)=>registrations.TryGetOnTheRunFuturesContract(symbol,out contract);
    /// <inheritdoc/>
    public bool TryGetFuturesTermStructureContracts(string symbol,out FuturesTermStructureContracts contracts)=>registrations.TryGetFuturesTermStructureContracts(symbol,out contracts);
    /// <inheritdoc/>
    public Task<bool> UpdateOnTheRunFuturesContractAsync(string symbol,DateOnly date,CancellationToken token=default,bool forceProviderRefresh=false)
        =>rollover.UpdateOnTheRunFuturesContractAsync(symbol,date,token,forceProviderRefresh);
    /// <inheritdoc/>
    public Task<bool> UpdateFuturesTermStructureContractsAsync(string symbol,DateOnly date,CancellationToken token=default)
        =>rollover.UpdateFuturesTermStructureContractsAsync(symbol,date,token);
}
