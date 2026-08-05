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
    public static ValueTask<DefaultFuturesContractDefinitionsReadModel> GetDefaultFuturesContractDefinitionsAsync(
        this GetDefaultFuturesContractDefinitionsQuery q, IDbContextFactory dbFactory)
        => GetDefaultFuturesContractDefinitionsAsync(dbFactory.ReferenceDb);

    internal static async ValueTask<DefaultFuturesContractDefinitionsReadModel> GetDefaultFuturesContractDefinitionsAsync(IReferenceDbContext db)
    {
        var currency = db.GetLookupTypeAsync("DefaultFuturesContractCurrency");
        var exchange = db.GetLookupTypeAsync("DefaultFuturesContractExchange");
        var multiplier = db.GetLookupTypeAsync("DefaultFuturesContractMultiplier");
        var securityType = db.GetLookupTypeAsync("DefaultFuturesContractSecurityType");
        var optionSecurityType = db.GetLookupTypeAsync("DefaultFuturesOptionContractSecurityType");
        var symbol = db.GetLookupTypeAsync("DefaultFuturesContractSymbol");
        var values = await Task.WhenAll(currency, exchange, multiplier, securityType, optionSecurityType, symbol)
            .ConfigureAwait(false);

        return new DefaultFuturesContractDefinitionsReadModel
        {
            Currency = values[0].FirstOrDefault()?.ShortCode ?? string.Empty,
            Exchange = values[1].FirstOrDefault()?.ShortCode ?? string.Empty,
            Multiplier = values[2].FirstOrDefault()?.ShortCode ?? string.Empty,
            SecurityType = values[3].FirstOrDefault()?.ShortCode ?? string.Empty,
            OptionSecurityType = values[4].FirstOrDefault()?.ShortCode ?? string.Empty,
            Symbol = values[5].FirstOrDefault()?.ShortCode ?? string.Empty
        };
    }
}
