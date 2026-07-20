using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Shared.Reference.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Reference.Queries;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Reference;
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
    readonly IActorService _actorService = IsArgumentNull.Set( actorService);
    readonly IBlackboardService _blackboardService = IsArgumentNull.Set(blackboardService); 

    /// <summary>
    /// Returns <see langword="true"/> if the specified currency short code exists in the lookup type map.
    /// </summary>
    /// <param name="shortCode">The currency short code to search for.</param>
    /// <returns><see langword="true"/> if the currency short code exists; otherwise, <see langword="false"/>.</returns>
    public bool CurrencyExists(string shortCode)
    {
        var lookupTypeMap = GetLookupTypeMapFromCache();
        return lookupTypeMap != null 
            && lookupTypeMap.ContainsKey("Currency") 
            && lookupTypeMap["Currency"].Any(e => e.Equals(shortCode, StringComparison.CurrentCultureIgnoreCase));
    }

    /// <summary>
    /// Returns <see langword="true"/> if the specified exchange short code exists in the lookup type map.
    /// </summary>
    /// <param name="shortCode">The exchange short code to search for.</param>
    /// <returns><see langword="true"/> if the exchange short code exists; otherwise, <see langword="false"/>.</returns>
    public bool ExchangeExists(string shortCode)
    {
        var lookupTypeMap = GetLookupTypeMapFromCache();
        return lookupTypeMap != null 
            && lookupTypeMap.ContainsKey("Exchange") 
            && lookupTypeMap["Exchange"].Any(e => e.Equals(shortCode, StringComparison.CurrentCultureIgnoreCase));
    }

    /// <summary>
    /// Returns <see langword="true"/> if the specified multiplier short code exists in the lookup type map.
    /// </summary>
    /// <param name="shortCode">The multiplier short code to search for.</param>
    /// <returns><see langword="true"/> if the multiplier short code exists; otherwise, <see langword="false"/>.</returns>
    public bool MultiplierExists(string shortCode)
    {
        var lookupTypeMap = GetLookupTypeMapFromCache();
        return lookupTypeMap != null 
            && lookupTypeMap.ContainsKey("Multiplier") 
            && lookupTypeMap["Multiplier"].Any(e => e.Equals(shortCode, StringComparison.CurrentCultureIgnoreCase));
    }

    /// <summary>
    /// Returns <see langword="true"/> if the specified security type short code exists in the lookup type map.
    /// </summary>
    /// <param name="shortCode">The security type short code to search for.</param>
    /// <returns><see langword="true"/> if the security type short code exists; otherwise, <see langword="false"/>.</returns>
    public bool SecurityTypeExists(string shortCode)
    {
        var lookupTypeMap = GetLookupTypeMapFromCache();
        return lookupTypeMap != null 
            && lookupTypeMap.ContainsKey("SecurityType") 
            && lookupTypeMap["SecurityType"].Any(e => e.Equals(shortCode, StringComparison.CurrentCultureIgnoreCase));
    }

    /// <summary>
    /// Returns <see langword="true"/> if the specified symbol short code exists in the lookup type map.
    /// </summary>
    /// <param name="shortCode">The symbol short code to search for.</param>
    /// <returns><see langword="true"/> if the symbol short code exists; otherwise, <see langword="false"/>.</returns>
    public bool SymbolExists(string shortCode)
    {
        var lookupTypeMap = GetLookupTypeMapFromCache();
        return lookupTypeMap != null 
            && lookupTypeMap.ContainsKey("Symbol") 
            && lookupTypeMap["Symbol"].Any(e => e.Equals(shortCode, StringComparison.CurrentCultureIgnoreCase));
    }

    /// <summary>
    /// Retrieves the lookup type map from the blackboard cache, populating it from the actor service if not already cached.
    /// </summary>
    /// <returns>A dictionary mapping lookup type names to their associated short codes.</returns>
    Dictionary<string, List<string>>  GetLookupTypeMapFromCache()
    {
        var lookupTypeMap = _blackboardService.ReferenceLookup.Get();
        if (lookupTypeMap is null)
        {
            AddLookupTypeMapToCache();
            lookupTypeMap = _blackboardService.ReferenceLookup.Get();
        }
        return lookupTypeMap ?? [];
        
        void AddLookupTypeMapToCache()
        {
            var serviceResult = _actorService.RequestAsync<LookupTypeCollection, GetLookupTypesQuery>(
                new GetLookupTypesQuery
                {
                    Subject = new ActorSubject(ActorType.Query, GetLookupTypesQuery.Actor, GetLookupTypesQuery.Verb, ActorEntityId.Default.Format()),
                    EntityId = ActorEntityId.Default
                }).Result;
            if (serviceResult is not null && serviceResult.Value is not null)
            {
                var lookupTypeMap = new Dictionary<string, List<string>>();
                foreach (var e in serviceResult.Value)
                {
                    if (!lookupTypeMap.ContainsKey(e.LookupTypeName))
                        lookupTypeMap.Add(e.LookupTypeName, []);
                    lookupTypeMap[e.LookupTypeName].Add(e.ShortCode);
                }
                _blackboardService.ReferenceLookup.Set(lookupTypeMap);
            }
        }
    }

}
