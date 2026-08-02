using TomasAI.IFM.Domain.Reference.Shared.Queries;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Reference.Query;

public static class GetDefaultFuturesContractDefinitions
{
    /// <summary>
    /// Handles a request to retrieve default futures contract definitions.
    /// </summary>
    public static async ValueTask<DefaultFuturesContractDefinitionsReadModel> GetDefaultFuturesContractDefinitionsAsync(
        this GetDefaultFuturesContractDefinitionsQuery q, IDbContextFactory dbFactory)
    {
        return await GetDefaultFuturesContractDefinitionsAsync(dbFactory.ReferenceDb);

        static async ValueTask<DefaultFuturesContractDefinitionsReadModel> GetDefaultFuturesContractDefinitionsAsync(IReferenceDbContext db)
            => new()
            {
                Currency = (await db.GetLookupTypeAsync("DefaultFuturesContractCurrency")).FirstOrDefault()?.ShortCode ?? string.Empty,
                Exchange = (await db.GetLookupTypeAsync("DefaultFuturesContractExchange")).FirstOrDefault()?.ShortCode ?? string.Empty,
                Multiplier = (await db.GetLookupTypeAsync("DefaultFuturesContractMultiplier")).FirstOrDefault()?.ShortCode ?? string.Empty,
                SecurityType = (await db.GetLookupTypeAsync("DefaultFuturesContractSecurityType")).FirstOrDefault()?.ShortCode ?? string.Empty,
                OptionSecurityType = (await db.GetLookupTypeAsync("DefaultFuturesOptionContractSecurityType")).FirstOrDefault()?.ShortCode ?? string.Empty,
                Symbol = (await db.GetLookupTypeAsync("DefaultFuturesContractSymbol")).FirstOrDefault()?.ShortCode ?? string.Empty
            };
    }
}
