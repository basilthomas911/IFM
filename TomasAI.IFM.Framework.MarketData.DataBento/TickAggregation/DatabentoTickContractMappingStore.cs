using System.Collections.Concurrent;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;

namespace TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation;

/// <summary>
/// Definition-date-scoped, explicit provider-instrument to domain-contract map.
/// </summary>
public sealed class DatabentoTickContractMappingStore : ITickContractMappingStore
{
    private readonly ConcurrentDictionary<MappingKey, TickContractMapping> _mappings = [];

    public void SetTickMapping(
        string dataset,
        DateOnly definitionDate,
        ushort publisherId,
        uint instrumentId,
        string contractId,
        AssetTypeId assetTypeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataset);
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
        if (definitionDate == default)
            throw new ArgumentOutOfRangeException(nameof(definitionDate));
        if (instrumentId == 0)
            throw new ArgumentOutOfRangeException(nameof(instrumentId));
        if (assetTypeId is not (AssetTypeId.Futures or AssetTypeId.FuturesOption))
            throw new ArgumentOutOfRangeException(nameof(assetTypeId));

        var key = new MappingKey(dataset, definitionDate, publisherId, instrumentId);
        var mapping = new TickContractMapping(
            dataset,
            definitionDate,
            publisherId,
            instrumentId,
            contractId,
            assetTypeId);
        if (!_mappings.TryAdd(key, mapping)
            && _mappings[key] != mapping)
        {
            throw new InvalidOperationException(
                $"A conflicting tick mapping exists for {publisherId}:{instrumentId} " +
                $"on {definitionDate:yyyy-MM-dd}.");
        }
    }

    public bool TryGetMapping(
        string dataset,
        DateOnly definitionDate,
        InstrumentKey instrument,
        out TickContractMapping mapping) =>
        _mappings.TryGetValue(
            new MappingKey(dataset, definitionDate, instrument.PublisherId, instrument.InstrumentId),
            out mapping);

    private readonly record struct MappingKey(
        string Dataset,
        DateOnly DefinitionDate,
        ushort PublisherId,
        uint InstrumentId);
}
