using TomasAI.IFM.Domain.Reference.Shared.Queries;
using System.Collections.Frozen;
using TomasAI.IFM.Domain.Reference.Shared;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Reference.Services;

/// <summary>
/// Provides reference lookup functionality for currencies, exchanges, multipliers, security types, and symbols using
/// actor and blackboard services.
/// </summary>
/// <remarks>ReferenceLookupActorService maintains a cache of lookup type mappings to optimize repeated existence
/// checks. The cache is automatically populated from the actor service if not already available. This service is
/// thread-safe for concurrent existence checks.</remarks>
/// <param name="actorService">The actor service used to query reference data from external sources.</param>
/// <param name="blackboardService">The blackboard service used to cache and retrieve reference lookup data.</param>
public class ReferenceLookupActorService(IActorService actorService,  IBlackboardService blackboardService)
    : IReferenceLookupService
{
    const long LocalCacheLifetimeMilliseconds = 30_000;
    static readonly FrozenDictionary<string, FrozenSet<string>> EmptyLookupIndex =
        new Dictionary<string, FrozenSet<string>>(StringComparer.Ordinal)
            .ToFrozenDictionary(StringComparer.Ordinal);
    readonly IActorService _actorService = IsArgumentNull.Set( actorService);
    readonly IBlackboardService _blackboardService = IsArgumentNull.Set(blackboardService);
    readonly SemaphoreSlim _refreshGate = new(1, 1);
    FrozenDictionary<string, FrozenSet<string>>? _lookupIndex;
    long _expiresAt;
    long _observedGeneration = -1;

    /// <summary>
    /// Returns <see langword="true"/> if the specified currency short code exists in the lookup type map.
    /// </summary>
    /// <param name="shortCode">The currency short code to search for.</param>
    /// <returns><see langword="true"/> if the currency short code exists; otherwise, <see langword="false"/>.</returns>
    public bool CurrencyExists(string shortCode) => Exists("Currency", shortCode);

    /// <summary>
    /// Returns <see langword="true"/> if the specified exchange short code exists in the lookup type map.
    /// </summary>
    /// <param name="shortCode">The exchange short code to search for.</param>
    /// <returns><see langword="true"/> if the exchange short code exists; otherwise, <see langword="false"/>.</returns>
    public bool ExchangeExists(string shortCode) => Exists("Exchange", shortCode);

    /// <summary>
    /// Returns <see langword="true"/> if the specified multiplier short code exists in the lookup type map.
    /// </summary>
    /// <param name="shortCode">The multiplier short code to search for.</param>
    /// <returns><see langword="true"/> if the multiplier short code exists; otherwise, <see langword="false"/>.</returns>
    public bool MultiplierExists(string shortCode) => Exists("Multiplier", shortCode);

    /// <summary>
    /// Returns <see langword="true"/> if the specified security type short code exists in the lookup type map.
    /// </summary>
    /// <param name="shortCode">The security type short code to search for.</param>
    /// <returns><see langword="true"/> if the security type short code exists; otherwise, <see langword="false"/>.</returns>
    public bool SecurityTypeExists(string shortCode) => Exists("SecurityType", shortCode);

    /// <summary>
    /// Returns <see langword="true"/> if the specified symbol short code exists in the lookup type map.
    /// </summary>
    /// <param name="shortCode">The symbol short code to search for.</param>
    /// <returns><see langword="true"/> if the symbol short code exists; otherwise, <see langword="false"/>.</returns>
    public bool SymbolExists(string shortCode) => Exists("Symbol", shortCode);

    public async ValueTask EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        var now = Environment.TickCount64;
        var generation = ReferenceLookupCacheGeneration.Current;
        var lookupIndex = Volatile.Read(ref _lookupIndex);
        if (lookupIndex is not null
            && generation == Volatile.Read(ref _observedGeneration)
            && now < Volatile.Read(ref _expiresAt))
            return;

        await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            now = Environment.TickCount64;
            generation = ReferenceLookupCacheGeneration.Current;
            lookupIndex = _lookupIndex;
            if (lookupIndex is not null
                && generation == _observedGeneration
                && now < _expiresAt)
                return;

            var lookupTypeMap = _blackboardService.Reference.ReferenceLookup.Get();
            if (lookupTypeMap is null)
            {
                var serviceResult = await _actorService.RequestAsync<LookupTypeCollection, GetLookupTypesQuery>(
                    new GetLookupTypesQuery
                    {
                        Subject = new ActorSubject(ActorType.Query, GetLookupTypesQuery.Actor, GetLookupTypesQuery.Verb, ActorEntityId.Default.Format()),
                        EntityId = ActorEntityId.Default
                    }, cancellationToken).ConfigureAwait(false);
                lookupTypeMap = serviceResult?.Value is { } values
                    ? CreateLookupTypeMap(values)
                    : [];
                if (lookupTypeMap.Count > 0)
                    _blackboardService.Reference.ReferenceLookup.Set(lookupTypeMap);
            }

            Publish(lookupTypeMap, generation, now);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    /// <summary>
    /// Retrieves the lookup type map from the blackboard cache, populating it from the actor service if not already cached.
    /// </summary>
    /// <returns>A dictionary mapping lookup type names to their associated short codes.</returns>
    bool Exists(string lookupTypeName, string shortCode)
    {
        if (shortCode is null)
            return false;
        var lookupIndex = Volatile.Read(ref _lookupIndex);
        if (lookupIndex is null)
        {
            var lookupTypeMap = _blackboardService.Reference.ReferenceLookup.Get();
            if (lookupTypeMap is null)
                return false;
            lookupIndex = Publish(lookupTypeMap, ReferenceLookupCacheGeneration.Current, Environment.TickCount64);
        }
        return lookupIndex.TryGetValue(lookupTypeName, out var shortCodes)
            && shortCodes.Contains(shortCode);
    }

    FrozenDictionary<string, FrozenSet<string>> Publish(
        Dictionary<string, List<string>> lookupTypeMap,
        long generation,
        long now)
    {
        var lookupIndex = Freeze(lookupTypeMap);
        Volatile.Write(ref _lookupIndex, lookupIndex);
        Volatile.Write(ref _observedGeneration, generation);
        Volatile.Write(ref _expiresAt, now + LocalCacheLifetimeMilliseconds);
        return lookupIndex;
    }

    static Dictionary<string, List<string>> CreateLookupTypeMap(LookupTypeCollection values)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            if (!map.TryGetValue(value.LookupTypeName, out var shortCodes))
            {
                shortCodes = [];
                map.Add(value.LookupTypeName, shortCodes);
            }
            shortCodes.Add(value.ShortCode);
        }
        return map;
    }

    static FrozenDictionary<string, FrozenSet<string>> Freeze(Dictionary<string, List<string>> map)
    {
        if (map.Count == 0)
            return EmptyLookupIndex;

        var index = new Dictionary<string, FrozenSet<string>>(map.Count, StringComparer.Ordinal);
        foreach (var pair in map)
        {
            index[pair.Key] = pair.Value.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        }
        return index.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
