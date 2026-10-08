using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>Maps retained option source evidence to the existing position-price input without recalculating Greeks.</summary>
public static class OptionTradeRealtimeEvent
{
    /// <summary>Creates an idempotent option trade event preserving contract, session, sequence and native timestamps.</summary>
    /// <param name="evidence">The validated source record acknowledged by durable storage.</param>
    /// <returns>The raw trade event consumed by option position routing.</returns>
    /// <remarks>Source evidence does not carry side/action/header flags; zero represents unavailable metadata.
    /// This event is a price input, not a broker execution or a trade decision.</remarks>
    public static FuturesTickTradeDataChangedEvent ToRealtimeEvent(this OptionTradeEvidence evidence)
    {
        evidence.Validate();
        var source = evidence.Source;
        var sequence = checked((uint)source.Sequence);
        var observed = DateTime.UnixEpoch.AddTicks(source.ReceiveNanoseconds / 100);
        var entity = new TickDataEntityId(source.ContractId, source.ValueDate, AssetTypeId.FuturesOption);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(source.Identity));
        var id = new Guid(hash.AsSpan(0, 16));
        return new()
        {
            Subject = new(ActorType.Realtime, FuturesTickTradeDataChangedEvent.Actor,
                FuturesTickTradeDataChangedEvent.Verb, entity.Format()),
            Id = id,
            CommandId = id,
            EntityId = entity,
            AggregateId = entity.Format(),
            EventSource = nameof(OptionTradeRealtimeEvent),
            ReceivedOn = observed,
            TickDataId = new(source.ContractId, source.ValueDate, source.Sequence, observed),
            AssetTypeId = AssetTypeId.FuturesOption,
            Dataset = source.Dataset,
            DefinitionDate = source.ValueDate,
            PublisherId = source.PublisherId,
            InstrumentId = source.InstrumentId,
            SourceDataset = source.Dataset,
            SourceGenerationId = source.GenerationId,
            OptionMarketPriceObservation = new(evidence.Context?.Contract.UnderlyingContractId ?? string.Empty, OptionMarketPriceBasis.Trade,
                new FuturesOptionTickDataV2ReadModel(source.ContractId, source.ValueDate, source.Sequence,
                    TimeOnly.FromDateTime(DateTime.UnixEpoch.AddTicks(source.EventNanoseconds / 100)), (double)source.Price,
                    0, 0, 0, 0, evidence.Greeks?.ImpliedVolatility ?? 0,
                    evidence.Underlying is { } underlying ? (double)((underlying.Bid + underlying.Ask) / 2m) : 0,
                    evidence.Greeks?.Delta ?? 0, evidence.Greeks?.Gamma ?? 0, evidence.Greeks?.Vega ?? 0,
                    evidence.Greeks?.Theta ?? 0, evidence.Greeks?.Rho ?? 0) { GreeksAvailable = evidence.Greeks is not null },
                source.Sequence, DateTime.UnixEpoch.AddTicks(source.EventNanoseconds / 100)),
            TradeData = new(sequence, source.EventNanoseconds, source.ReceiveNanoseconds, 0,
                checked((long)(source.Price * 1_000_000_000m)), source.Price, source.Size, 0, 0, 0)
        };
    }
}
