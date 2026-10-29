using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Rust.Api;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
namespace TomasAI.IFM.Application.MarketData.Rust.Host;

public sealed partial class NativeOwnerApplicationServices
{
    // A single date-scoped memo of detached cold metadata. The lazy holder starts
    // no work before it wins admission. It never owns a live provider resource;
    // cancellation/expiry of cold queries cannot pin the separate dataset owner.
    sealed record ColdCatalog(DateOnly Date,Lazy<Task<NativeContractCatalog>> Value);
    ColdCatalog? catalog;
    async Task<NativeContractCatalog> Catalog(CancellationToken token=default)
    {
        var date=Date;ColdCatalog selected;
        while(true)
        {
            var previous=Volatile.Read(ref catalog);
            if(previous is not null&&previous.Date==date&&(!previous.Value.IsValueCreated||!previous.Value.Value.IsFaulted)) {selected=previous;break;}
            var next=new ColdCatalog(date,new(()=>NativeContractCatalog.CreateAsync(cold.HistoricalRuntime,
                options with{Contracts=registrations.Snapshot()},clock,TimeSpan.FromMinutes(2)),LazyThreadSafetyMode.ExecutionAndPublication));
            if(ReferenceEquals(Interlocked.CompareExchange(ref catalog,next,previous),previous)){selected=next;break;}
        }
        return await selected.Value.Value.WaitAsync(token).ConfigureAwait(false);
    }
    static void Range(DateOnly from,DateOnly through)
    {ArgumentOutOfRangeException.ThrowIfEqual(from,default);ArgumentOutOfRangeException.ThrowIfEqual(through,default);ArgumentOutOfRangeException.ThrowIfLessThan(through,from);}
    /// <inheritdoc/>
    public async Task<FuturesContractV3ReadModel?> GetFuturesContractAsync(string contract)
    {
        _=Date;ArgumentException.ThrowIfNullOrWhiteSpace(contract);
        if(Runtime.ReadDatasetOwnerBinding(contract,true) is not null)throw new MarketDataContractKindMismatchException(contract,"futures","futures option");
        foreach(var lane in new[]{Delivery.MarketDataLane.FuturesGlbx,Delivery.MarketDataLane.FuturesXcbf})
            if(Runtime.ReadDatasetOwnerContracts(lane).FirstOrDefault(row=>row.ContractId==contract) is{} value)return value;
        return (await Catalog().ConfigureAwait(false)).FindFutures(contract);
    }
    /// <inheritdoc/>
    public async Task<FuturesContractV3ReadModel[]> GetFuturesContractsAsync(string[] contracts)
    {
        ArgumentNullException.ThrowIfNull(contracts);_=Date;
        var result=new FuturesContractV3ReadModel[contracts.Length];var missing=new List<string>();
        for(var index=0;index<contracts.Length;index++){
            var value=await GetFuturesContractAsync(contracts[index]).ConfigureAwait(false);
            if(value is null)missing.Add(contracts[index]);else result[index]=value;
        }
        if(missing.Count!=0)throw new MarketDataBatchResolutionException(missing);
        return result;
    }
    /// <inheritdoc/>
    public async Task<FuturesOptionContractReadModel?> GetFuturesOptionContractAsync(string contract)
    {
        _=Date;ArgumentException.ThrowIfNullOrWhiteSpace(contract);
        if(Runtime.ReadDatasetOwnerBinding(contract,false) is not null)throw new MarketDataContractKindMismatchException(contract,"futures option","futures");
        // Preserve the current API's active-catalog lookup semantics. A row in
        // the securities database is not proof that this session configured it.
        // Provider parsing/projection stays in the independent native cold owner.
        var source=await Catalog().ConfigureAwait(false);
        if(source.FindFutures(contract) is not null)throw new MarketDataContractKindMismatchException(contract,"futures option","futures");
        return source.FindFuturesOption(contract);
    }
    /// <inheritdoc/>
    public async Task<FuturesOptionContractReadModel[]> GetFuturesOptionContractsAsync(string[] contracts)
    {
        ArgumentNullException.ThrowIfNull(contracts);_=Date;
        var result=new FuturesOptionContractReadModel[contracts.Length];var missing=new List<string>();
        for(var index=0;index<contracts.Length;index++){
            var value=await GetFuturesOptionContractAsync(contracts[index]).ConfigureAwait(false);
            if(value is null)missing.Add(contracts[index]);else result[index]=value;
        }
        if(missing.Count!=0)throw new MarketDataBatchResolutionException(missing);
        return result;
    }
    /// <inheritdoc/>
    public async Task<FuturesOptionContractReadModel[]> GetFuturesOptionChainContractsAsync(string contract,DateOnly maturity)
    {Range(maturity,maturity);return await(await Catalog().ConfigureAwait(false)).GetOptionChainAsync(contract,maturity).ConfigureAwait(false);}
    /// <inheritdoc/>
    public async Task<FuturesOptionContractReadModel[]> GetFuturesOptionChainContractsByRootAsync(string symbol,string root,DateOnly maturity)
    {Range(maturity,maturity);return await(await Catalog().ConfigureAwait(false)).GetOptionChainByRootAsync(symbol,root,maturity).ConfigureAwait(false);}
    /// <inheritdoc/>
    public async Task<FuturesOptionContractReadModel[]> GetFuturesOptionChainContractsByRootAsync(string symbol,string root,DateOnly from,DateOnly through,CancellationToken token=default)
    {Range(from,through);return await(await Catalog(token).ConfigureAwait(false)).GetOptionChainByRootAsync(symbol,root,from,through,token).ConfigureAwait(false);}
    /// <inheritdoc/>
    public async Task<FuturesOptionContractReadModel[]> GetFuturesOptionChainContractsBySymbolAsync(string symbol,DateOnly from,DateOnly through)
    {
        Range(from,through);if(through.DayNumber-from.DayNumber>730)throw new ArgumentOutOfRangeException(nameof(through),"Option-chain ranges cannot exceed two years.");
        var source=await Catalog().ConfigureAwait(false);var result=new List<FuturesOptionContractReadModel>();
        for(var maturity=from;;maturity=maturity.AddDays(1)){
            result.AddRange(await source.GetOptionChainBySymbolAsync(symbol,maturity).ConfigureAwait(false));
            if(maturity==through)break;
        }
        return result.DistinctBy(row=>row.ContractId).OrderBy(row=>row.ContractMonth).ThenBy(row=>row.StrikePrice).ThenBy(row=>row.OptionType,StringComparer.Ordinal).ToArray();
    }
    /// <inheritdoc/>
    public async Task<OptionContractExpiryReadModel[]> DiscoverOptionContractExpiriesAsync(string symbol,DateOnly from,DateOnly through,CancellationToken token=default)
    {Range(from,through);return await(await Catalog(token).ConfigureAwait(false)).DiscoverOptionContractExpiriesAsync(symbol,from,through,token).ConfigureAwait(false);}
}
