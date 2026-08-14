using System.Globalization;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.Blackboard;

/// <summary>
/// Stores current-day Databento contract-ID/instrument-ID pairs in both directions.
/// </summary>
public interface IDatabentoContractMappingCache
{
    bool TryGetInstrumentId(
        string dataset,
        string contractId,
        out uint instrumentId);

    bool TryGetContractId(
        string dataset,
        uint instrumentId,
        out string? contractId);

    void SetMapping(
        string dataset,
        string contractId,
        uint instrumentId,
        ContractMappingDirection sourceDirection);

    void ClearMapping(string dataset, string contractId);

    void ClearMapping(string dataset, uint instrumentId);

    void ClearCurrentMappings(string dataset);
}

/// <summary>
/// Redis-backed Databento mapping cache with a 24-hour hard expiration and a
/// 15-minute sliding time-to-live. Keys are isolated by dataset and UTC date
/// because Databento instrument IDs can be remapped between trading days.
/// </summary>
public sealed class DatabentoContractMappingCache : IDatabentoContractMappingCache, ITickContractMappingStore
{
    public static readonly TimeSpan AbsoluteExpiration = TimeSpan.FromHours(24);
    public static readonly TimeSpan SlidingTimeToLive = TimeSpan.FromMinutes(15);

    private const string CacheName = "DatabentoContractMapping:v1";
    private readonly IRedisCache _redisCache;
    private readonly IJsonSerializer _jsonSerializer;
    private readonly TimeProvider _timeProvider;

    public DatabentoContractMappingCache(
        IRedisCache redisCache,
        IJsonSerializer jsonSerializer,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(redisCache);
        ArgumentNullException.ThrowIfNull(jsonSerializer);

        _redisCache = redisCache;
        _jsonSerializer = jsonSerializer;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public bool TryGetInstrumentId(
        string dataset,
        string contractId,
        out uint instrumentId)
    {
        ValidateDataset(dataset);
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);

        instrumentId = default;
        var definitionDate = CurrentDefinitionDate();
        var key = ContractKey(dataset, definitionDate, contractId);
        var entry = ReadEntry(key);
        if (entry is null)
        {
            return false;
        }
        if (!MatchesPartition(entry, dataset, definitionDate)
            || !string.Equals(entry.ContractId, contractId, StringComparison.Ordinal)
            || entry.InstrumentId == 0)
        {
            ThrowConflict(
                ContractMappingDirection.ContractIdToInstrumentId,
                dataset,
                definitionDate,
                contractId,
                null,
                entry,
                "The cached contract-ID key contains a different or invalid mapping.");
        }

        EnsureCounterpart(
            entry,
            InstrumentKey(dataset, definitionDate, entry.InstrumentId),
            ContractMappingDirection.ContractIdToInstrumentId);
        BestEffortRefresh(entry);
        instrumentId = entry.InstrumentId;
        return true;
    }

    public bool TryGetContractId(
        string dataset,
        uint instrumentId,
        out string? contractId)
    {
        ValidateDataset(dataset);
        if (instrumentId == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(instrumentId),
                "Databento instrument IDs must be positive.");
        }

        contractId = null;
        var definitionDate = CurrentDefinitionDate();
        var key = InstrumentKey(dataset, definitionDate, instrumentId);
        var entry = ReadEntry(key);
        if (entry is null)
        {
            return false;
        }
        if (!MatchesPartition(entry, dataset, definitionDate)
            || entry.InstrumentId != instrumentId
            || string.IsNullOrWhiteSpace(entry.ContractId))
        {
            ThrowConflict(
                ContractMappingDirection.InstrumentIdToContractId,
                dataset,
                definitionDate,
                null,
                instrumentId,
                entry,
                "The cached instrument-ID key contains a different or invalid mapping.");
        }

        EnsureCounterpart(
            entry,
            ContractKey(dataset, definitionDate, entry.ContractId),
            ContractMappingDirection.InstrumentIdToContractId);
        BestEffortRefresh(entry);
        contractId = entry.ContractId;
        return true;
    }

    public void SetMapping(
        string dataset,
        string contractId,
        uint instrumentId,
        ContractMappingDirection sourceDirection)
    {
        ValidateDataset(dataset);
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
        if (instrumentId == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(instrumentId),
                "Databento instrument IDs must be positive.");
        }

        var now = _timeProvider.GetUtcNow();
        var definitionDate = DateOnly.FromDateTime(now.UtcDateTime);
        var contractKey = ContractKey(dataset, definitionDate, contractId);
        var instrumentKey = InstrumentKey(dataset, definitionDate, instrumentId);
        var byContract = ReadEntry(contractKey);
        var byInstrument = ReadEntry(instrumentKey);

        if (byContract is not null && !IsSameMapping(
                byContract, dataset, definitionDate, contractId, instrumentId))
        {
            ThrowConflict(
                sourceDirection,
                dataset,
                definitionDate,
                contractId,
                instrumentId,
                byContract,
                "The live mapping conflicts with the cached contract-ID mapping.");
        }
        if (byInstrument is not null && !IsSameMapping(
                byInstrument, dataset, definitionDate, contractId, instrumentId))
        {
            ThrowConflict(
                sourceDirection,
                dataset,
                definitionDate,
                contractId,
                instrumentId,
                byInstrument,
                "The live mapping conflicts with the cached instrument-ID mapping.");
        }

        var absoluteExpirationUtc = now + AbsoluteExpiration;
        if (byContract is not null && byContract.AbsoluteExpirationUtc < absoluteExpirationUtc)
        {
            absoluteExpirationUtc = byContract.AbsoluteExpirationUtc;
        }
        if (byInstrument is not null && byInstrument.AbsoluteExpirationUtc < absoluteExpirationUtc)
        {
            absoluteExpirationUtc = byInstrument.AbsoluteExpirationUtc;
        }
        var entry = new DatabentoContractMappingCacheEntry
        {
            Dataset = dataset,
            DefinitionDate = definitionDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
            ContractId = contractId,
            InstrumentId = instrumentId,
            AbsoluteExpirationUtc = absoluteExpirationUtc
        };
        WritePair(entry);
    }

    public void SetTickMapping(
        string dataset,
        DateOnly definitionDate,
        ushort publisherId,
        uint instrumentId,
        string contractId,
        AssetTypeId assetTypeId,
        TickerContractDetails? contractDetails = null)
    {
        ValidateDataset(dataset);
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
        if (publisherId == 0 || instrumentId == 0 || assetTypeId == AssetTypeId.Unknown)
            throw new ArgumentOutOfRangeException(nameof(instrumentId), "Publisher, instrument, and asset type must be defined.");
        var now = _timeProvider.GetUtcNow();
        var entry = new DatabentoContractMappingCacheEntry
        {
            Dataset = dataset,
            DefinitionDate = definitionDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
            ContractId = contractId,
            InstrumentId = instrumentId,
            PublisherId = publisherId,
            AssetTypeId = assetTypeId,
            AbsoluteExpirationUtc = now + AbsoluteExpiration
        };
        var remaining = entry.AbsoluteExpirationUtc - now;
        _redisCache.Set(
            TickInstrumentKey(dataset, definitionDate, publisherId, instrumentId),
            _jsonSerializer.Serialize(entry),
            entry.AbsoluteExpirationUtc,
            remaining < SlidingTimeToLive ? remaining : SlidingTimeToLive);
        WritePair(entry);
    }

    public bool TryGetMapping(
        string dataset,
        DateOnly definitionDate,
        InstrumentKey instrument,
        out TickContractMapping mapping)
    {
        ValidateDataset(dataset);
        var entry = ReadEntry(TickInstrumentKey(dataset, definitionDate, instrument.PublisherId, instrument.InstrumentId));
        if (entry is null || entry.PublisherId != instrument.PublisherId ||
            entry.InstrumentId != instrument.InstrumentId || entry.AssetTypeId == AssetTypeId.Unknown ||
            !MatchesPartition(entry, dataset, definitionDate))
        {
            mapping = default;
            return false;
        }
        mapping = new TickContractMapping(
            dataset, definitionDate, entry.PublisherId, entry.InstrumentId,
            entry.ContractId, entry.AssetTypeId);
        return true;
    }

    public void ClearMapping(string dataset, string contractId)
    {
        ValidateDataset(dataset);
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);

        var definitionDate = CurrentDefinitionDate();
        var contractKey = ContractKey(dataset, definitionDate, contractId);
        var entry = ReadEntry(contractKey);
        _redisCache.Remove(contractKey);
        if (entry is not null
            && MatchesPartition(entry, dataset, definitionDate)
            && string.Equals(entry.ContractId, contractId, StringComparison.Ordinal))
        {
            _redisCache.Remove(InstrumentKey(dataset, definitionDate, entry.InstrumentId));
        }
    }

    public void ClearMapping(string dataset, uint instrumentId)
    {
        ValidateDataset(dataset);
        if (instrumentId == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(instrumentId),
                "Databento instrument IDs must be positive.");
        }

        var definitionDate = CurrentDefinitionDate();
        var instrumentKey = InstrumentKey(dataset, definitionDate, instrumentId);
        var entry = ReadEntry(instrumentKey);
        _redisCache.Remove(instrumentKey);
        if (entry is not null
            && MatchesPartition(entry, dataset, definitionDate)
            && entry.InstrumentId == instrumentId)
        {
            _redisCache.Remove(ContractKey(dataset, definitionDate, entry.ContractId));
        }
    }

    public void ClearCurrentMappings(string dataset)
    {
        ValidateDataset(dataset);
        _redisCache.RemoveByPrefix(PartitionPrefix(dataset, CurrentDefinitionDate()));
    }

    private DatabentoContractMappingCacheEntry? ReadEntry(string key)
    {
        var value = _redisCache.Get(key);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        DatabentoContractMappingCacheEntry? entry;
        try
        {
            entry = _jsonSerializer.Deserialize<DatabentoContractMappingCacheEntry>(value);
        }
        catch
        {
            BestEffortRemove(key);
            return null;
        }
        if (entry is null
            || string.IsNullOrWhiteSpace(entry.Dataset)
            || string.IsNullOrWhiteSpace(entry.DefinitionDate)
            || string.IsNullOrWhiteSpace(entry.ContractId)
            || entry.InstrumentId == 0)
        {
            BestEffortRemove(key);
            return null;
        }
        if (entry.AbsoluteExpirationUtc <= _timeProvider.GetUtcNow())
        {
            BestEffortEvict(entry);
            return null;
        }
        return entry;
    }

    private void EnsureCounterpart(
        DatabentoContractMappingCacheEntry entry,
        string counterpartKey,
        ContractMappingDirection direction)
    {
        var counterpart = ReadEntry(counterpartKey);
        var definitionDate = ParseDefinitionDate(entry.DefinitionDate);
        if (counterpart is not null && !IsSameMapping(
                counterpart,
                entry.Dataset,
                definitionDate,
                entry.ContractId,
                entry.InstrumentId))
        {
            ThrowConflict(
                direction,
                entry.Dataset,
                definitionDate,
                entry.ContractId,
                entry.InstrumentId,
                counterpart,
                "The two cached mapping directions disagree.");
        }
    }

    private void WritePair(DatabentoContractMappingCacheEntry entry)
    {
        var remaining = entry.AbsoluteExpirationUtc - _timeProvider.GetUtcNow();
        if (remaining <= TimeSpan.Zero)
        {
            BestEffortEvict(entry);
            return;
        }
        var definitionDate = ParseDefinitionDate(entry.DefinitionDate);
        var serialized = _jsonSerializer.Serialize(entry);
        var contractKey = ContractKey(entry.Dataset, definitionDate, entry.ContractId);
        var instrumentKey = InstrumentKey(entry.Dataset, definitionDate, entry.InstrumentId);
        try
        {
            _redisCache.Set(
                contractKey,
                serialized,
                entry.AbsoluteExpirationUtc,
                SlidingTimeToLive);
            _redisCache.Set(
                instrumentKey,
                serialized,
                entry.AbsoluteExpirationUtc,
                SlidingTimeToLive);
        }
        catch
        {
            BestEffortRemove(contractKey);
            BestEffortRemove(instrumentKey);
            throw;
        }
    }

    private void BestEffortRefresh(DatabentoContractMappingCacheEntry entry)
    {
        try
        {
            WritePair(entry);
        }
        catch
        {
            // A valid cache hit remains usable if Redis cannot renew its TTL.
        }
    }

    private void ThrowConflict(
        ContractMappingDirection direction,
        string dataset,
        DateOnly definitionDate,
        string? requestedContractId,
        uint? requestedInstrumentId,
        DatabentoContractMappingCacheEntry conflictingEntry,
        string reason)
    {
        BestEffortEvict(conflictingEntry);
        if (!string.IsNullOrWhiteSpace(requestedContractId))
        {
            BestEffortRemove(ContractKey(dataset, definitionDate, requestedContractId));
        }
        if (requestedInstrumentId is > 0)
        {
            BestEffortRemove(InstrumentKey(dataset, definitionDate, requestedInstrumentId.Value));
        }
        throw new DatabentoContractMappingException(
            direction,
            $"{reason} Dataset '{dataset}', definition date {definitionDate:yyyy-MM-dd}, "
            + $"requested contract ID '{requestedContractId ?? "<none>"}', requested instrument ID "
            + $"{requestedInstrumentId?.ToString(CultureInfo.InvariantCulture) ?? "<none>"}; cached "
            + $"contract ID '{conflictingEntry.ContractId}', cached instrument ID "
            + $"{conflictingEntry.InstrumentId}.",
            requestedContractId,
            requestedInstrumentId);
    }

    private void BestEffortEvict(DatabentoContractMappingCacheEntry entry)
    {
        if (!DateOnly.TryParseExact(
                entry.DefinitionDate,
                "yyyyMMdd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var definitionDate))
        {
            return;
        }
        BestEffortRemove(ContractKey(entry.Dataset, definitionDate, entry.ContractId));
        BestEffortRemove(InstrumentKey(entry.Dataset, definitionDate, entry.InstrumentId));
    }

    private void BestEffortRemove(string key)
    {
        try
        {
            _redisCache.Remove(key);
        }
        catch
        {
            // Eviction must not conceal the original mapping/cache result.
        }
    }

    private DateOnly CurrentDefinitionDate() =>
        DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);

    private static bool MatchesPartition(
        DatabentoContractMappingCacheEntry entry,
        string dataset,
        DateOnly definitionDate) =>
        string.Equals(entry.Dataset, dataset, StringComparison.Ordinal)
        && string.Equals(
            entry.DefinitionDate,
            definitionDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
            StringComparison.Ordinal);

    private static bool IsSameMapping(
        DatabentoContractMappingCacheEntry entry,
        string dataset,
        DateOnly definitionDate,
        string contractId,
        uint instrumentId) =>
        MatchesPartition(entry, dataset, definitionDate)
        && string.Equals(entry.ContractId, contractId, StringComparison.Ordinal)
        && entry.InstrumentId == instrumentId;

    private static DateOnly ParseDefinitionDate(string definitionDate) =>
        DateOnly.ParseExact(
            definitionDate,
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None);

    private static string ContractKey(
        string dataset,
        DateOnly definitionDate,
        string contractId) =>
        $"{CacheName}:{Uri.EscapeDataString(dataset)}:{definitionDate:yyyyMMdd}:contract:{contractId}";

    private static string PartitionPrefix(
        string dataset,
        DateOnly definitionDate) =>
        $"{CacheName}:{Uri.EscapeDataString(dataset)}:{definitionDate:yyyyMMdd}:";

    private static string InstrumentKey(
        string dataset,
        DateOnly definitionDate,
        uint instrumentId) =>
        $"{CacheName}:{Uri.EscapeDataString(dataset)}:{definitionDate:yyyyMMdd}:instrument:"
        + instrumentId.ToString(CultureInfo.InvariantCulture);

    private static string TickInstrumentKey(
        string dataset,
        DateOnly definitionDate,
        ushort publisherId,
        uint instrumentId) =>
        $"{CacheName}:{Uri.EscapeDataString(dataset)}:{definitionDate:yyyyMMdd}:tick-instrument:"
        + publisherId.ToString(CultureInfo.InvariantCulture) + ":"
        + instrumentId.ToString(CultureInfo.InvariantCulture);

    private static void ValidateDataset(string dataset) =>
        ArgumentException.ThrowIfNullOrWhiteSpace(dataset);
}

public sealed record DatabentoContractMappingCacheEntry
{
    public required string Dataset { get; init; }
    public required string DefinitionDate { get; init; }
    public required string ContractId { get; init; }
    public uint InstrumentId { get; init; }
    public ushort PublisherId { get; init; }
    public AssetTypeId AssetTypeId { get; init; }
    public DateTimeOffset AbsoluteExpirationUtc { get; init; }
}
