using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento.OptionChain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>Publishes real two-sided marks for individually subscribed monitoring legs through the generation-owned publisher.</summary>
public sealed class IndividualOptionObservationPublisher(
    Guid generation, OptionChainPricingInputStore inputs, ITickAggregationEventPublisher publisher,
    Func<string, bool> isIndividual, TimeProvider? time = null) : IOptionChainTransientEventPublisher
{
    readonly TimeProvider clock = time ?? TimeProvider.System;
    readonly Dictionary<string, (decimal Bid, decimal Ask, uint BidSize, uint AskSize, long Timestamp)> publishedQuotes = new(StringComparer.Ordinal);
    long revision;

    /// <summary>Maps midpoint = bid + (ask - bid) / 2; invalid or chain-wide quotes do not enter position monitoring.</summary>
    /// <param name="quote">The actual provider quote with its canonical option identity.</param>
    /// <returns>The publication enqueue operation, with no pricing or storage wait. Repeated unchanged quotes are suppressed for at most one second; price and size changes are emitted.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the existing public contract and named argument compatibility; changing parameter names or order would break callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the existing public contract and named argument compatibility; changing parameter names or order would break callers.")]
    public ValueTask PublishAsync(FuturesOptionChainQuoteChangedServiceEvent quote) => PublishQuoteAsync(quote, false);

    /// <summary>Publishes asynchronous calculation completion even when no subsequent quote arrives.</summary>
    public ValueTask PublishPricingAsync(FuturesOptionChainQuoteChangedServiceEvent quote) => PublishQuoteAsync(quote, true);

    ValueTask PublishQuoteAsync(FuturesOptionChainQuoteChangedServiceEvent quote, bool pricingUpdate)
    {
        var tick = quote.Tick;
        if (!isIndividual(tick.ContractId) || tick.SourceSequence <= 0 || !tick.TryGetMidpoint(out var mark)
            || !inputs.TryGet(tick.ContractId, out var input) || input is null
            || input.Context.GenerationId != generation) return ValueTask.CompletedTask;
        var timestamp = clock.GetTimestamp();
        lock (publishedQuotes)
        {
            if (!pricingUpdate && publishedQuotes.TryGetValue(tick.ContractId, out var previous)
                && previous.Bid == tick.BidPrice && previous.Ask == tick.AskPrice
                && previous.BidSize == tick.BidSize && previous.AskSize == tick.AskSize
                && clock.GetElapsedTime(previous.Timestamp, timestamp) < TimeSpan.FromSeconds(1))
                return ValueTask.CompletedTask;
            // Every changed bid/ask or size is emitted. A continuing unchanged provider quote emits once per second
            // to keep position freshness current without persisting every depth/size update as a new mark.
            if (!pricingUpdate) publishedQuotes[tick.ContractId] = (tick.BidPrice!.Value, tick.AskPrice!.Value, tick.BidSize, tick.AskSize, timestamp);
        }
        var contract = input.Context.Contract;
        var entity = new TickDataEntityId(tick.ContractId, tick.ValueDate, AssetTypeId.FuturesOption);
        var greeks = quote.Greeks;
        var deadline = tick.EventTimestamp.AddMilliseconds(input.Context.MaximumQuoteAgeMilliseconds);
        if (input.Context.ValidUntilUtc < deadline) deadline = input.Context.ValidUntilUtc;
        if (greeks.FuturesPriceTimestamp != default)
        {
            var underlyingDeadline = greeks.FuturesPriceTimestamp.AddMilliseconds(input.Context.MaximumQuoteAgeMilliseconds);
            if (underlyingDeadline < deadline) deadline = underlyingDeadline;
        }
        var request = Black76PricingModel.CreateRequest(contract, 1, 1, true, 1, input.Context.Rate.AnnualContinuousRate);
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
                    ModelVersion = input.Context.PricerVersion
                } };
        return publisher.PublishAsync(new FuturesTickTradeDataChangedEvent
        {
            Subject = new(ActorType.Realtime, FuturesTickTradeDataChangedEvent.Actor, FuturesTickTradeDataChangedEvent.Verb, entity.Format()),
            Id = quote.EventId, CommandId = quote.EventId, EntityId = entity, AggregateId = entity.Format(),
            EventSource = nameof(IndividualOptionObservationPublisher), ReceivedOn = clock.GetUtcNow().UtcDateTime,
            TickDataId = new(tick.ContractId, tick.ValueDate, tick.SourceSequence, tick.EventTimestamp.UtcDateTime),
            AssetTypeId = AssetTypeId.FuturesOption, Dataset = contract.Dataset, DefinitionDate = tick.ValueDate,
            PublisherId = contract.PublisherId, InstrumentId = contract.InstrumentId,
            SourceDataset = contract.Dataset, SourceGenerationId = generation,
            OptionMarketPriceObservation = new(contract.UnderlyingContractId, OptionMarketPriceBasis.QuoteMidpoint,
                observation, tick.SourceSequence, tick.EventTimestamp.UtcDateTime)
        });
    }

    /// <summary>Actual trades are already published after their retained evidence is acknowledged; prevents duplicate routing.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the existing public contract and named argument compatibility; changing parameter names or order would break callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the existing public contract and named argument compatibility; changing parameter names or order would break callers.")]
    public ValueTask PublishAsync(FuturesOptionChainTradeChangedServiceEvent trade) => ValueTask.CompletedTask;
}
