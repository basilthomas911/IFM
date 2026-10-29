using System.Collections.Immutable;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using TomasAI.IFM.Shared.MarketData.Contracts.LastPrice;

namespace TomasAI.IFM.Application.MarketData.Rust.Api;

/// <summary>Complete-scope composition source over already-owned native state.
/// Immutable preparation binds context, contract set and logical generation.
/// Reads never create provider connections or calculate prices. Application
/// lease authority must select this source before a capture is permitted.</summary>
public sealed class NativeOptionCompositionSource : ICompositionMarketSource
{
    const uint Running = 3; // Stable C ABI: absent=0, stopped=1, starting=2, running=3, recovering=4, failed=5.
    readonly NativeMarketDataRuntime runtime;
    readonly NativeOptionPricingCache cache;
    readonly string scope, digest;
    readonly Guid generation;
    readonly ImmutableArray<PreparedOptionPricingContext> options;
    readonly TimeProvider clock;
    readonly OptionPricingRefreshPolicy policy;
    readonly Func<bool> isCurrent;
    /// <summary>Freezes a bounded, duplicate-free context batch for an owned
    /// scope. A successful atomic context replacement requires a new source.
    /// The authority predicate must be a nonblocking read of the owning scope
    /// revision and lifetime; it fences captures from retired preparation.</summary>
    public NativeOptionCompositionSource(NativeMarketDataRuntime runtime,string scope,string digest,Guid generation,
        ImmutableArray<PreparedOptionPricingContext> options,TimeProvider clock,OptionPricingRefreshPolicy policy,Func<bool> isCurrent)
    {
        ArgumentNullException.ThrowIfNull(runtime);ArgumentNullException.ThrowIfNull(clock);ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(isCurrent);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);ArgumentException.ThrowIfNullOrWhiteSpace(digest);policy.Validate();
        if(generation==Guid.Empty||options.IsDefaultOrEmpty||options.Length>2048||scope.Length>128||digest.Length>128
            ||options.Any(x=>x is null||x.Context.GenerationId!=generation)
            ||options.Select(x=>x.Context.Contract.ContractId).Distinct(StringComparer.Ordinal).Count()!=options.Length
            ||options.Select(x=>x.Context.Contract.InstrumentId).Distinct().Count()!=options.Length
            ||options.Select(x=>x.Context.Contract.UnderlyingContractId).Distinct(StringComparer.Ordinal).Count()!=1
            ||options.Select(x=>x.UnderlyingInstrument).Distinct().Count()!=1)throw new ArgumentException("A complete qualified physical options scope is required.",nameof(options));
        this.runtime=runtime;this.scope=scope;this.digest=digest;this.generation=generation;
        this.options=options.OrderBy(x=>x.Context.Contract.ContractId,StringComparer.Ordinal).ToImmutableArray();this.clock=clock;this.policy=policy;this.isCurrent=isCurrent;cache=new(runtime);
    }
    OptionPricingQuote Convert(LastQuoteTickSnapshot quote)=>new(quote.ContractId,quote.BidPrice??0,quote.AskPrice??0,quote.BidSize,quote.AskSize,
        quote.EventTimestamp,quote.LocalReceivedAtUtc==default?quote.ReceiveTimestamp:quote.LocalReceivedAtUtc,quote.SourceSequence,generation);
    /// <inheritdoc/>
    public Task<CompositionMarketPage> ReadAsync(CompositionSnapshotRequest request,string? continuation,CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);cancellationToken.ThrowIfCancellationRequested();
        if(request.GenerationId!=generation||request.ScopeId!=scope||continuation is not null||!request.IncludeOptions)
            throw new CompositionMarketSourceException("ChainUnavailable");
        if(!isCurrent()||runtime.ReadOptionOwnerGeneration()!=generation)throw new CompositionMarketSourceException("Recovering");
        var status=runtime.ReadOptionScopeStatus(scope);
        if(status.Phase!=Running)throw new CompositionMarketSourceException("Recovering");
        var at=clock.GetUtcNow();var first=options[0];
        var underlyingState=runtime.ReadOptionScopeState(scope,first.UnderlyingInstrument,first.Context.Contract.PublisherId);
        var underlying=underlyingState.TryGetLastQuote(first.Context.Contract.UnderlyingContractId,runtime.LiveValueDate!.Value,out var underlyingTick)?Convert(underlyingTick):null;
        if(underlying is null&&!request.AllowMissingOptionQuotes)throw new CompositionMarketSourceException("UnderlyingQuoteUnavailable");
        var values=ImmutableArray.CreateBuilder<CompositionMarketInstrument>(options.Length);
        foreach(var option in options)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var id=option.Context.Contract.ContractId;
            var state=runtime.ReadOptionScopeState(scope,option.Context.Contract.InstrumentId,option.Context.Contract.PublisherId);
            if(state.Flags==0||state.Source.GenerationLow!=underlyingState.Source.GenerationLow||state.Source.GenerationHigh!=underlyingState.Source.GenerationHigh)
                throw new CompositionMarketSourceException("Recovering");
            state.TryGetOptionStatistics(id,out var statistics);
            CompositionMarketInstrument item;
            if(request.SelectionOnly&&!request.RiskContractIds.Contains(id,StringComparer.Ordinal))
            {
                var selection=cache.ReadSelection(scope,generation,option,policy.Version,at);
                if(selection is {Failure:null,Delta:not null,Iv:not null})
                {
                    item=new(id,selection.Option,option.Context,option.Strike,option.IsCall,selection.Underlying)
                        {Selection=OptionSelectionValue.From(selection,policy.ImpliedVolatilityMilliseconds)};
                    values.Add(WithStatistics(item));continue;
                }
                if(!request.AllowMissingOptionQuotes)throw new CompositionMarketSourceException(selection?.Failure?.Code??"SelectionCalculationPending");
            }
            var quote=state.TryGetLastQuote(id,runtime.LiveValueDate!.Value,out var tick)?Convert(tick):null;
            if(quote is null&&!request.AllowMissingOptionQuotes)throw new CompositionMarketSourceException("QuoteUnavailable");
            item=new(id,quote,option.Context,option.Strike,option.IsCall,underlying);values.Add(WithStatistics(item));
            CompositionMarketInstrument WithStatistics(CompositionMarketInstrument value)=>value with
            {SessionVolume=statistics?.Volume,OpenInterest=statistics?.OpenInterest,StatisticsAtUtc=state.OptionStatisticsAtUtc,
                VolumeValueDate=statistics?.VolumeValueDate,OpenInterestValueDate=statistics?.OpenInterestValueDate};
        }
        cancellationToken.ThrowIfCancellationRequested();var final=runtime.ReadOptionScopeStatus(scope);
        if(!isCurrent()||runtime.ReadOptionOwnerGeneration()!=generation||final.Phase!=Running||final.Attempt!=status.Attempt||final.Generation!=status.Generation)
            throw new CompositionMarketSourceException("Recovering");
        return Task.FromResult(new CompositionMarketPage(digest,generation,values.Count,values.MoveToImmutable(),null));
    }
}


