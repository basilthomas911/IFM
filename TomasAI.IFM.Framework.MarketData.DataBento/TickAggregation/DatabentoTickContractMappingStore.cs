using System.Collections.Concurrent;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;

namespace TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation;

/// <summary>
/// Definition-date-scoped, explicit provider-instrument to domain-contract map.
/// </summary>
public sealed class DatabentoTickContractMappingStore : ITickContractMappingStore
{
    private readonly ConcurrentDictionary<MappingKey, TickContractMapping> _mappings = [];
    private readonly ConcurrentDictionary<SymbolMappingKey, TickContractMapping> _symbolMappings = [];

    /// <summary>Stores the dataset and definition-date-specific mapping from instrument to contract.</summary>
    /// <param name="dataset">The Databento dataset identifier.</param>
    /// <param name="definitionDate">The date of the instrument definition mapping.</param>
    /// <param name="publisherId">The provider publisher identifier.</param>
    /// <param name="instrumentId">The provider instrument identifier.</param>
    /// <param name="contractId">The futures or option contract identifier.</param>
    /// <param name="assetTypeId">The asset type identifying futures or futures options.</param>
    /// <param name="contractDetails">The reviewed reference details for the mapped contract.</param>
    public void SetTickMapping(
        string dataset,
        DateOnly definitionDate,
        ushort publisherId,
        uint instrumentId,
        string contractId,
        AssetTypeId assetTypeId,
        TickerContractDetails? contractDetails = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataset);
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
        if (definitionDate == default)
            throw new ArgumentOutOfRangeException(nameof(definitionDate));
        if (instrumentId == 0)
            throw new ArgumentOutOfRangeException(nameof(instrumentId));
        if (assetTypeId is not (AssetTypeId.Futures or AssetTypeId.FuturesOption))
            throw new ArgumentOutOfRangeException(nameof(assetTypeId));

        var key = new MappingKey(dataset, definitionDate, instrumentId);
        var candidate = new TickContractMapping(
            dataset,
            definitionDate,
            publisherId,
            instrumentId,
            contractId,
            assetTypeId,
            contractDetails);
        var mapping = _mappings.AddOrUpdate(
            key,
            candidate,
            (_, existing) =>
            {
                if (!string.Equals(existing.ContractId, candidate.ContractId, StringComparison.Ordinal)
                    || existing.AssetTypeId != candidate.AssetTypeId)
                {
                    throw new InvalidOperationException(
                        $"A conflicting tick mapping exists for instrument {instrumentId} " +
                        $"in dataset '{dataset}' on {definitionDate:yyyy-MM-dd}.");
                }
                return candidate.ContractDetails is not null || existing.ContractDetails is null
                    ? candidate
                    : candidate with
                    {
                        ContractDetails = WithInstrument(
                            existing.ContractDetails,
                            new InstrumentKey(candidate.PublisherId, candidate.InstrumentId))
                    };
            });

        if (contractDetails is not null)
        {
            SetSymbolMapping(dataset, definitionDate, contractDetails.ProviderContractId, mapping);
            SetSymbolMapping(dataset, definitionDate, contractDetails.LocalSymbol, mapping);
        }
    }

    /// <summary>Attempts to resolve the stored mapping for the specified instrument.</summary>
    /// <param name="dataset">The Databento dataset identifier.</param>
    /// <param name="definitionDate">The date of the instrument definition mapping.</param>
    /// <param name="instrument">The instrument.</param>
    /// <param name="mapping">The contract mapping to use or returned by a successful lookup.</param>
    /// <returns>True when the operation succeeds or the requested condition holds; otherwise, false.</returns>
    public bool TryGetMapping(
        string dataset,
        DateOnly definitionDate,
        InstrumentKey instrument,
        out TickContractMapping mapping) =>
        _mappings.TryGetValue(
            new MappingKey(dataset, definitionDate, instrument.InstrumentId),
            out mapping);

    /// <summary>Attempts to resolve a feed registration to its contract mapping.</summary>
    /// <param name="dataset">The Databento dataset identifier.</param>
    /// <param name="definitionDate">The date of the instrument definition mapping.</param>
    /// <param name="registration">The feed instrument registration whose contract mapping is required.</param>
    /// <param name="mapping">The contract mapping to use or returned by a successful lookup.</param>
    /// <returns>True when the operation succeeds or the requested condition holds; otherwise, false.</returns>
    public bool TryResolveFeedMapping(
        string dataset,
        DateOnly definitionDate,
        TickerInstrumentRegistration registration,
        out TickContractMapping mapping)
    {
        ArgumentNullException.ThrowIfNull(registration);
        if (TryGetMapping(dataset, definitionDate, registration.Instrument, out mapping))
        {
            if (mapping.PublisherId != registration.Instrument.PublisherId)
            {
                SetTickMapping(
                    dataset,
                    definitionDate,
                    registration.Instrument.PublisherId,
                    registration.Instrument.InstrumentId,
                    mapping.ContractId,
                    mapping.AssetTypeId,
                    WithInstrument(mapping.ContractDetails, registration.Instrument));
                _ = TryGetMapping(dataset, definitionDate, registration.Instrument, out mapping);
            }
            return true;
        }

        if (!TryGetSymbolMapping(dataset, definitionDate, registration.RawSymbol, out var catalogMapping)
            && !TryGetSymbolMapping(dataset, definitionDate, registration.RequestedSymbol, out catalogMapping))
            return false;

        var details = WithInstrument(catalogMapping.ContractDetails, registration.Instrument);
        var liveMapping = catalogMapping with
        {
            PublisherId = registration.Instrument.PublisherId,
            InstrumentId = registration.Instrument.InstrumentId,
            ContractDetails = details
        };
        var liveKey = new MappingKey(
            dataset,
            definitionDate,
            registration.Instrument.InstrumentId);
        if (!_mappings.TryAdd(liveKey, liveMapping))
        {
            var existing = _mappings[liveKey];
            if (!string.Equals(existing.ContractId, liveMapping.ContractId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Live instrument {registration.Instrument.PublisherId}:" +
                    $"{registration.Instrument.InstrumentId} resolves to conflicting contracts " +
                    $"'{existing.ContractId}' and '{liveMapping.ContractId}' on " +
                    $"{definitionDate:yyyy-MM-dd}.");
            }
            mapping = existing;
            return true;
        }

        mapping = liveMapping;
        return true;
    }

    private void SetSymbolMapping(
        string dataset,
        DateOnly definitionDate,
        string symbol,
        TickContractMapping mapping)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return;
        var key = new SymbolMappingKey(dataset, definitionDate, symbol);
        if (!_symbolMappings.TryAdd(key, mapping)
            && !string.Equals(
                _symbolMappings[key].ContractId,
                mapping.ContractId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Provider symbol '{symbol}' maps to conflicting contracts on " +
                $"{definitionDate:yyyy-MM-dd}.");
        }
    }

    private bool TryGetSymbolMapping(
        string dataset,
        DateOnly definitionDate,
        string symbol,
        out TickContractMapping mapping)
    {
        if (!string.IsNullOrWhiteSpace(symbol))
            return _symbolMappings.TryGetValue(
                new SymbolMappingKey(dataset, definitionDate, symbol),
                out mapping);
        mapping = default;
        return false;
    }

    /// <summary>Initializes a new MappingKey instance.</summary>
    /// <param name="Dataset">The dataset.</param>
    /// <param name="DefinitionDate">The definition date.</param>
    /// <param name="InstrumentId">The provider instrument identifier.</param>
    private readonly record struct MappingKey(
        string Dataset,
        DateOnly DefinitionDate,
        uint InstrumentId);

    private static TickerContractDetails? WithInstrument(
        TickerContractDetails? details,
        InstrumentKey instrument) =>
        details is null
            ? null
            : details with
            {
                PublisherId = instrument.PublisherId,
                InstrumentId = instrument.InstrumentId
            };

    /// <summary>Initializes a new SymbolMappingKey instance.</summary>
    /// <param name="Dataset">The dataset.</param>
    /// <param name="DefinitionDate">The definition date.</param>
    /// <param name="Symbol">The symbol.</param>
    private readonly record struct SymbolMappingKey(
        string Dataset,
        DateOnly DefinitionDate,
        string Symbol);
}
