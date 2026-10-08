using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;
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
    readonly Dictionary<string, (decimal Bid, decimal Ask, long Timestamp)> publishedQuotes = new(StringComparer.Ordinal);

    /// <summary>Maps midpoint = bid + (ask - bid) / 2; invalid or chain-wide quotes do not enter position monitoring.</summary>
    /// <param name="quote">The actual provider quote with its canonical option identity.</param>
    /// <returns>The publication enqueue operation, with no pricing or storage wait. Book-size-only changes are suppressed for at most one second.</returns>
    public ValueTask PublishAsync(FuturesOptionChainQuoteChangedServiceEvent quote)
    {
        var tick = quote.Tick;
        if (!isIndividual(tick.ContractId) || tick.SourceSequence <= 0 || !tick.TryGetMidpoint(out var mark)
            || !inputs.TryGet(tick.ContractId, out var input) || input is null
            || input.Context.GenerationId != generation) return ValueTask.CompletedTask;
        var timestamp = clock.GetTimestamp();
        lock (publishedQuotes)
        {
            if (publishedQuotes.TryGetValue(tick.ContractId, out var previous)
                && previous.Bid == tick.BidPrice && previous.Ask == tick.AskPrice
                && clock.GetElapsedTime(previous.Timestamp, timestamp) < TimeSpan.FromSeconds(1))
                return ValueTask.CompletedTask;
            // Every changed bid/ask is emitted. A continuing unchanged provider quote emits once per second
            // to keep position freshness current without persisting every depth/size update as a new mark.
            publishedQuotes[tick.ContractId] = (tick.BidPrice!.Value, tick.AskPrice!.Value, timestamp);
        }
        var contract = input.Context.Contract;
        var entity = new TickDataEntityId(tick.ContractId, tick.ValueDate, AssetTypeId.FuturesOption);
        var greeks = quote.Greeks;
        var observation = new FuturesOptionTickDataV2ReadModel(tick.ContractId, tick.ValueDate, tick.SourceSequence,
            TimeOnly.FromDateTime(tick.EventTimestamp.UtcDateTime), (double)mark,
            (double)tick.BidPrice!.Value, (double)tick.AskPrice!.Value, checked((int)tick.BidSize), checked((int)tick.AskSize),
            greeks.ImpliedVolatility ?? 0, (double)(greeks.FuturesPrice ?? 0), greeks.Delta ?? 0,
            greeks.Gamma ?? 0, greeks.Vega ?? 0, greeks.Theta ?? 0, greeks.Rho ?? 0)
            { GreeksAvailable = greeks.IsValid && !greeks.IsStale };
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
    public ValueTask PublishAsync(FuturesOptionChainTradeChangedServiceEvent trade) => ValueTask.CompletedTask;
}
