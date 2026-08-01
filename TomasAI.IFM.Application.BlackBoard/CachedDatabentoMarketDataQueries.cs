using System.Collections.Concurrent;
using TomasAI.IFM.Framework.MarketData.DataBento;

namespace TomasAI.IFM.Application.Blackboard;

/// <summary>
/// DI-facing Databento query contract whose mapping operations use the application Blackboard cache.
/// </summary>
public interface ICachedDatabentoMarketDataQueries : IDatabentoMarketDataQueries
{
}

/// <summary>
/// Adds bidirectional Blackboard caching to Databento contract mappings while passing contract-detail
/// queries directly to the provider query service.
/// </summary>
public sealed class CachedDatabentoMarketDataQueries : ICachedDatabentoMarketDataQueries
{
    private readonly IDatabentoMarketDataQueries _source;
    private readonly IDatabentoContractMappingCache _cache;
    private readonly string _dataset;
    private readonly ConcurrentDictionary<ContractLookupKey, Lazy<uint>> _contractIdMisses = new();
    private readonly ConcurrentDictionary<InstrumentLookupKey, Lazy<string>> _instrumentIdMisses = new();

    public CachedDatabentoMarketDataQueries(
        IDatabentoMarketDataQueries source,
        IDatabentoContractMappingCache cache,
        string dataset)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataset);

        _source = source;
        _cache = cache;
        _dataset = dataset;
    }

    public uint ContractIdToInstrumentId(
        string contractId,
        TimeSpan? timeout = null)
    {
        if (!string.IsNullOrWhiteSpace(contractId)
            && TryGetInstrumentId(contractId, out var cached))
        {
            return cached;
        }

        if (string.IsNullOrWhiteSpace(contractId))
        {
            return _source.ContractIdToInstrumentId(contractId, timeout);
        }
        var lookupKey = new ContractLookupKey(contractId, timeout);
        var lazy = _contractIdMisses.GetOrAdd(
            lookupKey,
            _ => new Lazy<uint>(
                () => ResolveContractId(contractId, timeout),
                LazyThreadSafetyMode.ExecutionAndPublication));
        try
        {
            return lazy.Value;
        }
        finally
        {
            _contractIdMisses.TryRemove(
                new KeyValuePair<ContractLookupKey, Lazy<uint>>(lookupKey, lazy));
        }
    }

    public string InstrumentIdToContractId(
        uint instrumentId,
        TimeSpan? timeout = null)
    {
        if (instrumentId != 0 && TryGetContractId(instrumentId, out var cached))
        {
            return cached!;
        }

        if (instrumentId == 0)
        {
            return _source.InstrumentIdToContractId(instrumentId, timeout);
        }
        var lookupKey = new InstrumentLookupKey(instrumentId, timeout);
        var lazy = _instrumentIdMisses.GetOrAdd(
            lookupKey,
            _ => new Lazy<string>(
                () => ResolveInstrumentId(instrumentId, timeout),
                LazyThreadSafetyMode.ExecutionAndPublication));
        try
        {
            return lazy.Value;
        }
        finally
        {
            _instrumentIdMisses.TryRemove(
                new KeyValuePair<InstrumentLookupKey, Lazy<string>>(lookupKey, lazy));
        }
    }

    public ContractDetail? GetContractDetail(
        string contractName,
        TimeSpan? timeout = null) =>
        _source.GetContractDetail(contractName, timeout);

    public IReadOnlyList<ContractDetail> GetContractDetails(
        string ticker,
        TimeSpan? timeout = null) =>
        _source.GetContractDetails(ticker, timeout);

    public IReadOnlyList<ContractDetail?> GetContractDetails(
        string[] contractNames,
        TimeSpan? timeout = null) =>
        _source.GetContractDetails(contractNames, timeout);

    private uint ResolveContractId(string contractId, TimeSpan? timeout)
    {
        if (TryGetInstrumentId(contractId, out var cached))
        {
            return cached;
        }
        var instrumentId = _source.ContractIdToInstrumentId(contractId, timeout);
        if (instrumentId == 0)
        {
            throw new DatabentoContractMappingException(
                ContractMappingDirection.ContractIdToInstrumentId,
                $"Contract ID '{contractId}' resolved to invalid Databento instrument ID 0; "
                + "the mapping was not cached.",
                contractId,
                instrumentId);
        }
        TrySetMapping(
            contractId,
            instrumentId,
            ContractMappingDirection.ContractIdToInstrumentId);
        return instrumentId;
    }

    private string ResolveInstrumentId(uint instrumentId, TimeSpan? timeout)
    {
        if (TryGetContractId(instrumentId, out var cached))
        {
            return cached!;
        }
        var contractId = _source.InstrumentIdToContractId(instrumentId, timeout);
        if (string.IsNullOrWhiteSpace(contractId))
        {
            throw new DatabentoContractMappingException(
                ContractMappingDirection.InstrumentIdToContractId,
                $"Databento instrument ID {instrumentId} resolved to an empty contract ID; "
                + "the mapping was not cached.",
                contractId,
                instrumentId);
        }
        TrySetMapping(
            contractId,
            instrumentId,
            ContractMappingDirection.InstrumentIdToContractId);
        return contractId;
    }

    private bool TryGetInstrumentId(string contractId, out uint instrumentId)
    {
        try
        {
            return _cache.TryGetInstrumentId(_dataset, contractId, out instrumentId);
        }
        catch (DatabentoContractMappingException)
        {
            throw;
        }
        catch
        {
            instrumentId = default;
            return false;
        }
    }

    private bool TryGetContractId(uint instrumentId, out string? contractId)
    {
        try
        {
            return _cache.TryGetContractId(_dataset, instrumentId, out contractId);
        }
        catch (DatabentoContractMappingException)
        {
            throw;
        }
        catch
        {
            contractId = null;
            return false;
        }
    }

    private void TrySetMapping(
        string contractId,
        uint instrumentId,
        ContractMappingDirection sourceDirection)
    {
        try
        {
            _cache.SetMapping(_dataset, contractId, instrumentId, sourceDirection);
        }
        catch (DatabentoContractMappingException)
        {
            throw;
        }
        catch
        {
            // A cache infrastructure failure must not invalidate a verified live mapping.
        }
    }

    private readonly record struct ContractLookupKey(
        string ContractId,
        TimeSpan? Timeout);

    private readonly record struct InstrumentLookupKey(
        uint InstrumentId,
        TimeSpan? Timeout);
}
