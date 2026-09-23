using System.Collections.Concurrent;
using System.Collections.Immutable;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Query;

static class QualifiedEvaluatedOptionChain
{
    static readonly ConcurrentDictionary<string, WorkerOptionChainRequest> Leases = new();
    static readonly ConcurrentDictionary<string, CachedWindowInputs> WindowInputs = new();
    static readonly ConcurrentDictionary<string, AtTheMoneyIv> ImpliedVolatility = new();
    sealed record CachedWindowInputs(Guid GenerationId, DateTimeOffset ExpiresAtUtc,
        string[] ProviderRoots, FuturesOptionContractReadModel[] Definitions, decimal? Price, decimal? Deviation);
    sealed record AtTheMoneyIv(Guid GenerationId, DateTimeOffset ObservedAtUtc, double Value);

    /// <summary>Returns a qualified, expiry-specific live chain, using ATM IV when available and a labelled Bollinger fallback otherwise.</summary>
    public static async Task<ServiceResult<EvaluatedOptionChainReadModel>> ExecuteAsync(
        GetEvaluatedOptionChainQuery query, IMarketDataQueryContext context, CancellationToken token)
    {
        var api=context.MarketDataApi;
        var discovery=context.CompositionDiscovery;
        var market=context.CompositionMarketData;
        var admissions=context.WorkerAdmissions;
        if(api is null||discovery is null||market is null||admissions is null)
            return new ServiceFailed<EvaluatedOptionChainReadModel>(503,"Qualified market-data runtime is unavailable.");
        var key=$"{query.UnderlyingContractId}|{query.ExpiryDate:yyyyMMdd}";
        if(query.ReleaseOnly)return await ReleaseAsync(key,query,discovery,token);
        if(!admissions.TryGet("GLBX.MDP3",out var admission))
            return new ServiceFailed<EvaluatedOptionChainReadModel>(503,"Market-data worker is not admitted.");
        return await AcquireAsync(key,query,context,api,discovery,market,admission,token);
    }
    static async Task<ServiceResult<EvaluatedOptionChainReadModel>> ReleaseAsync(string key,
        GetEvaluatedOptionChainQuery query,QualifiedCompositionDiscovery discovery,CancellationToken token)
    {
        WindowInputs.TryRemove(key,out _);
        ImpliedVolatility.TryRemove(key,out _);
        if(Leases.TryRemove(key,out var lease))await discovery.ReleaseAsync(lease,token);
        return new ServiceOk<EvaluatedOptionChainReadModel>(new(query.UnderlyingContractId,
            query.ExpiryDate,null,null,null,"Released",DateTimeOffset.UtcNow,[]));
    }
    static async Task<ServiceResult<EvaluatedOptionChainReadModel>> AcquireAsync(string key,
        GetEvaluatedOptionChainQuery query,IMarketDataQueryContext context,
        TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi api,
        QualifiedCompositionDiscovery discovery,ICompositionMarketDataApi market,
        DatasetWorkerAdmission admission,CancellationToken token)
    {
        var now=DateTimeOffset.UtcNow;
        var roots=query.ProviderRoots.Order(StringComparer.OrdinalIgnoreCase).ToArray();
        var cached=WindowInputs.TryGetValue(key,out var existing)
            &&existing.GenerationId==admission.GenerationId&&existing.ExpiresAtUtc>now
            &&existing.ProviderRoots.SequenceEqual(roots,StringComparer.OrdinalIgnoreCase)
            ?existing:null;
        decimal? livePrice;
        if(cached is null)
        {
            var definitions=await LoadDefinitionsAsync(query,context,token);
            var inputs=await OptionChainWindowInputs.GetAsync(
                context,query.UnderlyingSymbol,query.UnderlyingContractId,token);
            livePrice=inputs.Price;
            cached=new(admission.GenerationId,now.AddSeconds(30),roots,definitions,inputs.Price,inputs.Deviation);
            WindowInputs[key]=cached;
        }
        else livePrice=await api.GetFuturesPriceAsync(query.UnderlyingContractId).ConfigureAwait(false) ?? cached.Price;
        var windowPrice = cached.Price ?? livePrice;
        var expiry = cached.Definitions.Where(x => x.ExpirationUtc is not null)
            .Select(x => x.ExpirationUtc!.Value).DefaultIfEmpty().Min();
        var window = windowPrice is > 0 && expiry > now
            && ImpliedVolatility.TryGetValue(key, out var iv)
            && iv.GenerationId == admission.GenerationId && now - iv.ObservedAtUtc < TimeSpan.FromMinutes(5)
            ? OptionChainStrikeWindow.SelectImpliedVolatility(cached.Definitions,windowPrice.Value,
                iv.Value,expiry,now,requiredContractIds:query.RequiredContractIds)
            : OptionChainStrikeWindow.Select(cached.Definitions,windowPrice,cached.Deviation,
                query.StandardDeviationMultiplier,query.RequiredContractIds);
        if(window.Contracts.Length==0)return Failed(window.Method == "WindowInputsUnavailable"
            ? "Neither a qualified expiry IV nor current Bollinger window inputs are available."
            : "No contracts are available in the selected strike window.");
        var lease=await EnsureLeaseAsync(key,query,context,discovery,admission,window.Contracts,token);
        if(lease.Lease is null)return Failed($"The qualified option-chain lease could not be acquired: {lease.FailureCode}.");
        return await CaptureAsync(key,query,market,lease.Lease,window,livePrice ?? windowPrice,token);
    }
    static async Task<FuturesOptionContractReadModel[]> LoadDefinitionsAsync(
        GetEvaluatedOptionChainQuery query,IMarketDataQueryContext context,CancellationToken token)
    {
        var cached=await context.DbFactory.SecuritiesDb.GetCachedOptionContractDefinitionsAsync(
            query.UnderlyingSymbol,query.UnderlyingContractId,query.ExpiryDate,query.ProviderRoots,token);
        return cached.Select(row=>row.Definition).DistinctBy(x=>x.ContractId).ToArray();
    }
    static async Task<(WorkerOptionChainRequest? Lease, string? FailureCode)> EnsureLeaseAsync(string key,
        GetEvaluatedOptionChainQuery query,IMarketDataQueryContext context,
        QualifiedCompositionDiscovery discovery,DatasetWorkerAdmission admission,
        FuturesOptionContractReadModel[] contracts,CancellationToken token)
    {
        var now=DateTimeOffset.UtcNow;
        if(Leases.TryGetValue(key,out var current)&&current.GenerationId==admission.GenerationId
            && current.LeaseExpiresAtUtc>now.AddSeconds(10)&&SameContracts(current,contracts))return (current,null);
        if(current is not null&&current.GenerationId==admission.GenerationId
            &&current.LeaseExpiresAtUtc>now&&SameContracts(current,contracts))
        {
            var renewed=await discovery.RenewAsync(current,now.AddSeconds(60),token);
            if(renewed is not null){Leases[key]=renewed;return (renewed,null);}
            return (current,null);
        }
        var replacingSameContracts=current is not null&&current.GenerationId==admission.GenerationId
            &&current.LeaseExpiresAtUtc>now&&SameContracts(current,contracts);
        if(current is not null&&!replacingSameContracts)
        {Leases.TryRemove(key,out _);await discovery.ReleaseAsync(current,token);}
        var request=await DiscoveryRequestAsync(query,context,admission,contracts,now,token);
        if(request is null)return (null,"DiscoveryRequestUnavailable");
        var result=await discovery.AcquireAsync(request,token);
        if(result.Failure is not null||result.Lease is null)return (null,result.Failure?.Code??"NoQualifiedDefinitions");
        Leases[key]=result.Lease;
        if(replacingSameContracts)await discovery.ReleaseAsync(current!,token);
        return (result.Lease,null);
    }
    static bool SameContracts(WorkerOptionChainRequest lease,FuturesOptionContractReadModel[] contracts)
        => lease.Options.Select(x=>x.Pricing.Contract.ContractId).Order()
            .SequenceEqual(contracts.Select(x=>x.ContractId).Order(),StringComparer.Ordinal);
    static async Task<CompositionDiscoveryRequest?> DiscoveryRequestAsync(GetEvaluatedOptionChainQuery query,
        IMarketDataQueryContext context,DatasetWorkerAdmission admission,FuturesOptionContractReadModel[] contracts,
        DateTimeOffset now,CancellationToken token)
    {
        if(context.TreasuryPublication is null||context.TreasuryConversion is null)return null;
        var dates=await context.DbFactory.MarketDataDb.GetTradingDatesAsync(admission.ValueDate,
            query.ExpiryDate,MarketType.Futures,CurrencyType.USD,token);
        var first=contracts[0];
        var calendar=new OptionPricingCalendar(first.CalendarVersion??"IFM-MarketDates",
            first.ExchangeTimeZoneId??"America/New_York",admission.ValueDate,query.ExpiryDate,
            new TimeOnly(18,0),dates.ToImmutableArray());
        return new(Guid.NewGuid(),admission.GenerationId,admission.ValueDate,query.ExpiryDate,
            now.AddSeconds(60),contracts.Select(Candidate).ToArray(),true,calendar,
            context.TreasuryPublication,context.TreasuryConversion);
    }
    static OptionDefinitionCandidate Candidate(FuturesOptionContractReadModel value)
    {
        if(value.PublisherId is null||value.InstrumentId is null
            ||value.ExpirationUtc is null||value.MappingVersion is null||value.DefinitionDigest is null
            ||value.RawSymbol is null||value.Dataset is null||value.UnderlyingContractId is null)
            throw new InvalidDataException("Option definition is not reviewed for live pricing.");
        if(value.OptionRight is not (ReferenceOptionRight.Call or ReferenceOptionRight.Put))
            throw new InvalidDataException("Option definition has no qualified call/put right.");
        var definition=new OptionContractDefinition
        {
            Dataset=value.Dataset,RawSymbol=value.RawSymbol,Ticker=value.Symbol,
            Underlying=value.UnderlyingContractId,Instrument=new(value.PublisherId.Value,value.InstrumentId.Value),
            Right=value.OptionRight==ReferenceOptionRight.Call?OptionRightSelection.Call:OptionRightSelection.Put,
            StrikePrice=value.GetExactStrikePrice(),MaturityDate=DateOnly.FromDateTime(value.ExpirationUtc.Value.UtcDateTime),
            ExpirationTimestampNanoseconds=ToNanoseconds(value.ExpirationUtc.Value),
            ContractMultiplier=value.MultiplierValue is null?null:checked((int)value.MultiplierValue.Value)
        };
        return new(value.ContractId,value.MappingVersion,value.DefinitionDigest,definition);
    }
    static ulong ToNanoseconds(DateTimeOffset value)=>checked((ulong)
        (value.UtcTicks-DateTimeOffset.UnixEpoch.UtcTicks)*100UL);
    static async Task<ServiceResult<EvaluatedOptionChainReadModel>> CaptureAsync(
        string key,GetEvaluatedOptionChainQuery query,ICompositionMarketDataApi market,WorkerOptionChainRequest lease,
        OptionChainStrikeWindowResult window,decimal? price,CancellationToken token)
    {
        var deadline=DateTimeOffset.UtcNow.AddSeconds(5);
        while(DateTimeOffset.UtcNow<deadline)
        {
            var now=DateTimeOffset.UtcNow;
            var request=new CompositionSnapshotRequest(Guid.NewGuid(),lease.ScopeId,"Daily",
                lease.GenerationId,now,deadline,true) { AllowMissingOptionQuotes = true };
            var captured=await market.CaptureAsync("GLBX.MDP3",request,token);
            if(captured.Snapshot is not null)
            {
                ObserveAtTheMoneyIv(key,captured.Snapshot,price);
                return Success(query,captured.Snapshot,window,price);
            }
            if(captured.Failure?.Code is not ("QuoteUnavailable" or "UnderlyingQuoteUnavailable"))
                return Failed(captured.Failure?.Code??"Qualified snapshot is unavailable.");
            await Task.Delay(50,token);
        }
        return Failed("Qualified snapshot timed out.");
    }
    static void ObserveAtTheMoneyIv(string key,MarketCompositionSnapshot snapshot,decimal? price)
    {
        if(price is not > 0)return;
        // A live option quote may update every second; hold the window's ATM IV for a
        // bounded interval so small IV ticks do not replace the physical subscription.
        if(ImpliedVolatility.TryGetValue(key,out var prior)
            && prior.GenerationId==snapshot.GenerationId
            && snapshot.EvaluatedAtUtc-prior.ObservedAtUtc<TimeSpan.FromSeconds(30))return;
        var nearest = snapshot.Instruments.Where(x => x.Instrument.Strike is > 0
                && x.Instrument.Quote is not null && x.Valuation is not null
                && Math.Abs(x.Instrument.Strike!.Value-price.Value)<=price.Value*0.005m
                && double.IsFinite(x.Valuation.ImpliedVolatility) && x.Valuation.ImpliedVolatility > 0)
            .OrderBy(x => Math.Abs(x.Instrument.Strike!.Value-price.Value))
            .Take(2).ToArray();
        if(nearest.Length==0)return;
        ImpliedVolatility[key]=new(snapshot.GenerationId,snapshot.EvaluatedAtUtc,
            nearest.Average(x => x.Valuation!.ImpliedVolatility));
    }
    static ServiceResult<EvaluatedOptionChainReadModel> Success(GetEvaluatedOptionChainQuery query,
        MarketCompositionSnapshot snapshot,OptionChainStrikeWindowResult window,decimal? price)
    {
        var contracts=snapshot.Instruments.Select(ToReadModel).ToArray();
        var model=new EvaluatedOptionChainReadModel(query.UnderlyingContractId,query.ExpiryDate,
            price,window.LowerBound,window.UpperBound,window.Method,snapshot.EvaluatedAtUtc,contracts);
        return new ServiceOk<EvaluatedOptionChainReadModel>(model);
    }
    static ServiceResult<EvaluatedOptionChainReadModel> Failed(string message)
        => new ServiceFailed<EvaluatedOptionChainReadModel>(503,message);
    static EvaluatedOptionContractReadModel ToReadModel(CompositionInstrumentSnapshot item)
    {
        var instrument=item.Instrument;
        var quote=instrument.Quote;
        var value=item.Valuation;
        return new(instrument.ContractId,instrument.Strike!.Value,instrument.IsCall!.Value,
            quote?.Bid,quote?.Ask,quote is null?null:checked((uint)quote.BidSize),quote is null?null:checked((uint)quote.AskSize),null,null,
            value?.ImpliedVolatility,value?.TheoreticalPrice,value?.Delta,value?.Gamma,value?.Vega,
            value?.Theta,value?.Rho,instrument.SessionVolume,instrument.OpenInterest,value is not null,
            false,quote?.EventAtUtc,null,item.Valuation is null?null:quote?.ReceivedAtUtc);
    }
}
