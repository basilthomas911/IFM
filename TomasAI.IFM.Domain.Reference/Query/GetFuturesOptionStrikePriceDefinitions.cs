using TomasAI.IFM.Domain.Reference.Shared.Queries;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Reference.Query;

public static class GetFuturesOptionStrikePriceDefinitions
{
    /// <summary>
    /// Handles the GetFuturesOptionStrikePriceDefinitionsQuery by retrieving the minimum, maximum, and increment values for futures option strike prices from the reference database and replying with the results.
    /// </summary>
    /// <param name="q">The query to handle.</param>
    /// <param name="context">The query actor context.</param>
    /// <param name="dbFactory">The database context factory.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static ValueTask<FuturesOptionStrikePriceReadModel> GetFuturesOptionStrikePriceDefinitionsAsync(
        this GetFuturesOptionStrikePriceDefinitionsQuery q, IDbContextFactory dbFactory)
        => GetFuturesOptionStrikePriceDefinitionsAsync(dbFactory.ReferenceDb);

    internal static async ValueTask<FuturesOptionStrikePriceReadModel> GetFuturesOptionStrikePriceDefinitionsAsync(IReferenceDbContext db)
    {
        var minimum = db.GetLookupTypeAsync("FuturesOptionStrikePriceMin");
        var maximum = db.GetLookupTypeAsync("FuturesOptionStrikePriceMax");
        var increment = db.GetLookupTypeAsync("FuturesOptionStrikePriceIncrement");
        var values = await Task.WhenAll(minimum, maximum, increment).ConfigureAwait(false);

        return new FuturesOptionStrikePriceReadModel
        {
            Minimum = Convert.ToInt32(values[0].FirstOrDefault()?.ShortCode),
            Maximum = Convert.ToInt32(values[1].FirstOrDefault()?.ShortCode),
            Increment = Convert.ToInt32(values[2].FirstOrDefault()?.ShortCode)
        };
    }
}
