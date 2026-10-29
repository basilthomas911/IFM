using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento.OptionChain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;

namespace TomasAI.IFM.Application.MarketData.Rust.Publication;

/// <summary>Preserves the current application observation mapping, midpoint,
/// premium tick and actor envelope policy. Source Greeks are native; no solver
/// or provider I/O runs here. One instance owns the monotonic observation revision
/// for its complete logical generation, across all scopes and reference updates.</summary>
public sealed class NativeOptionObservationPublisher
{
    readonly Guid initialGeneration;
    readonly Func<Guid>? currentGeneration;
    Guid generation=>currentGeneration?.Invoke()??initialGeneration;
    readonly ITickAggregationEventPublisher publisher;
    readonly Func<string,bool> isIndividual;
    readonly TimeProvider clock;
    readonly Dictionary<string,(decimal Bid,decimal Ask,uint BidSize,uint AskSize,long Timestamp)> publishedQuotes=new(StringComparer.Ordinal);
    long revision;
    Guid publishedGeneration;
    /// <summary>Binds one complete logical generation to its existing publisher,
    /// nonblocking monitoring-ownership predicate and authoritative API clock.</summary>
    public NativeOptionObservationPublisher(Guid generation,ITickAggregationEventPublisher publisher,Func<string,bool> isIndividual,TimeProvider time,Func<Guid>? currentGeneration=null)
    {
        if(generation==Guid.Empty)throw new ArgumentException("A logical options generation is required.",nameof(generation));
        this.initialGeneration=generation;this.currentGeneration=currentGeneration;this.publisher=publisher??throw new ArgumentNullException(nameof(publisher));
        this.isIndividual=isIndividual??throw new ArgumentNullException(nameof(isIndividual));clock=time??throw new ArgumentNullException(nameof(time));
    }
    /// <summary>Prunes retired monitoring fingerprints at an application scope
    /// transition. The generation-wide observation revision never resets.</summary>
    public void RetainContracts(IReadOnlyCollection<string> contractIds)
    {
        ArgumentNullException.ThrowIfNull(contractIds);
        if(contractIds.Count>2048)throw new ArgumentOutOfRangeException(nameof(contractIds));
        var retained=contractIds.ToHashSet(StringComparer.Ordinal);
        lock(publishedQuotes)foreach(var id in publishedQuotes.Keys.Where(id=>!retained.Contains(id)).ToArray())publishedQuotes.Remove(id);
    }
    /// <summary>Maps one exact native quote/calculation to the existing actor
    /// event contract. Context is the frozen application metadata for this result;
    /// numerical values are already calculated in Rust. Acknowledging this method
    /// means publisher acceptance, not durable trade-evidence acknowledgment.</summary>
    public ValueTask PublishQuoteAsync(FuturesOptionChainQuoteChangedServiceEvent quote, OptionPricingContext context, bool pricingUpdate, CancellationToken cancellationToken=default)
    {
        ArgumentNullException.ThrowIfNull(context);cancellationToken.ThrowIfCancellationRequested();
        var generation=this.generation;
        var tick = quote.Tick;
        if (!isIndividual(tick.ContractId) || tick.SourceSequence <= 0 || !tick.TryGetMidpoint(out var mark)
            || context.GenerationId != generation || context.Contract.ContractId != tick.ContractId) return ValueTask.CompletedTask;
        var timestamp = clock.GetTimestamp();
        lock (publishedQuotes)
        {
            if(publishedGeneration!=generation){publishedQuotes.Clear();publishedGeneration=generation;}
            if(!pricingUpdate&&!publishedQuotes.ContainsKey(tick.ContractId)&&publishedQuotes.Count>=2048)
                throw new InvalidOperationException("Native option monitoring fingerprint capacity exhausted.");
            if (!pricingUpdate && publishedQuotes.TryGetValue(tick.ContractId, out var previous)
                && previous.Bid == tick.BidPrice && previous.Ask == tick.AskPrice
                && previous.BidSize == tick.BidSize && previous.AskSize == tick.AskSize
                && clock.GetElapsedTime(previous.Timestamp, timestamp) < TimeSpan.FromSeconds(1))
                return ValueTask.CompletedTask;
            // Every changed bid/ask or size is emitted. A continuing unchanged provider quote emits once per second
            // to keep position freshness current without persisting every depth/size update as a new mark.
            if (!pricingUpdate) publishedQuotes[tick.ContractId] = (tick.BidPrice!.Value, tick.AskPrice!.Value, tick.BidSize, tick.AskSize, timestamp);
        }
        var contract = context.Contract;
        var entity = new TickDataEntityId(tick.ContractId, tick.ValueDate, AssetTypeId.FuturesOption);
        var greeks = quote.Greeks;
        var deadline = tick.EventTimestamp.AddMilliseconds(context.MaximumQuoteAgeMilliseconds);
        if (context.ValidUntilUtc < deadline) deadline = context.ValidUntilUtc;
        if (greeks.FuturesPriceTimestamp != default)
        {
            var underlyingDeadline = greeks.FuturesPriceTimestamp.AddMilliseconds(context.MaximumQuoteAgeMilliseconds);
            if (underlyingDeadline < deadline) deadline = underlyingDeadline;
        }
        var request = Black76PricingModel.CreateRequest(contract, 1, 1, true, 1, context.Rate.AnnualContinuousRate);
        var observation = new FuturesOptionTickDataV2ReadModel(tick.ContractId, tick.ValueDate, tick.SourceSequence,
            TimeOnly.FromDateTime(tick.EventTimestamp.UtcDateTime), (double)mark,
            (double)tick.BidPrice!.Value, (double)tick.AskPrice!.Value, checked((int)tick.BidSize), checked((int)tick.AskSize),
            greeks.ImpliedVolatility ?? 0, (double)(greeks.FuturesPrice ?? 0), greeks.Delta ?? 0,
            greeks.Gamma ?? 0, greeks.Vega ?? 0, greeks.Theta ?? 0, greeks.Rho ?? 0)
            { GreeksAvailable = greeks.IsValid && !greeks.IsStale,
                SourceGeneration = generation, ObservationRevision = Interlocked.Increment(ref revision),
                ObservedAtUtc = tick.EventTimestamp, CalculatedAtUtc = greeks.CalculatedAtUtc,
                ValidUntilUtc = deadline, UnderlyingSourceSequence = greeks.FuturesPriceSourceSequence,
                PricingFailure = greeks.PricingFailure?.Code, PricingContextDigest = greeks.PricingContextDigest,
                IsPricingUpdate = pricingUpdate, TheoreticalPrice = greeks.TheoreticalPrice,
                PricingInputs = new()
                {
                    Currency = contract.Currency, Multiplier = contract.Multiplier, ExpirationUtc = contract.ExpirationUtc,
                    DaysPerYear = contract.DayCount == TomasAI.IFM.Shared.MarketData.Contracts.Pricing.PricingDayCount.Actual360 ? 360 : 365,
                    AnnualContinuousRate = request.Rate, Exercise = request.Exercise.ToString(), Premium = request.Premium.ToString(),
                    BidTickIncrement = TomasAI.IFM.Shared.MarketData.Pricing.OptionPremiumTicks.GetIncrement(contract, tick.BidPrice.Value, combinationLeg: true),
                    AskTickIncrement = TomasAI.IFM.Shared.MarketData.Pricing.OptionPremiumTicks.GetIncrement(contract, tick.AskPrice.Value, combinationLeg: true),
                    ModelVersion = context.PricerVersion
                } };
        return publisher.PublishAsync(new FuturesTickTradeDataChangedEvent
        {
            Subject = new(ActorType.Realtime, FuturesTickTradeDataChangedEvent.Actor, FuturesTickTradeDataChangedEvent.Verb, entity.Format()),
            Id = quote.EventId, CommandId = quote.EventId, EntityId = entity, AggregateId = entity.Format(),
            EventSource = nameof(TomasAI.IFM.Application.MarketData.Pricing.IndividualOptionObservationPublisher), ReceivedOn = clock.GetUtcNow().UtcDateTime,
            TickDataId = new(tick.ContractId, tick.ValueDate, tick.SourceSequence, tick.EventTimestamp.UtcDateTime),
            AssetTypeId = AssetTypeId.FuturesOption, Dataset = contract.Dataset, DefinitionDate = tick.ValueDate,
            PublisherId = contract.PublisherId, InstrumentId = contract.InstrumentId,
            SourceDataset = contract.Dataset, SourceGenerationId = generation,
            OptionMarketPriceObservation = new(contract.UnderlyingContractId, OptionMarketPriceBasis.QuoteMidpoint,
                observation, tick.SourceSequence, tick.EventTimestamp.UtcDateTime)
        },cancellationToken);
    }

}
