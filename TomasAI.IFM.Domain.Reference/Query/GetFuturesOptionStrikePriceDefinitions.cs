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

    /// <param name="q">
    /// The query to handle.
    /// </param>
    extension(GetFuturesOptionStrikePriceDefinitionsQuery q)
    {
        /// <summary>
        /// Handles the GetFuturesOptionStrikePriceDefinitionsQuery by retrieving the minimum, maximum, and increment values for futures option strike prices from the reference database and replying with the results.
        /// </summary>
        ///
        /// <param name="context">The query actor context.</param>
        /// <param name="dbFactory">The database context factory.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public ValueTask<FuturesOptionStrikePriceReadModel> ExecuteAsync(
        IDbContextFactory dbFactory, CancellationToken cancellationToken = default)

        {
            ArgumentNullException.ThrowIfNull(dbFactory);
            return GetFuturesOptionStrikePriceDefinitionsAsync(dbFactory.ReferenceDb, cancellationToken);
        }
    }

    internal static async ValueTask<FuturesOptionStrikePriceReadModel> GetFuturesOptionStrikePriceDefinitionsAsync(
        IReferenceDbContext db,
        CancellationToken cancellationToken = default)
    {
        Task<ICollection<LookupTypeReadModel>> ReadAsync(string name) => db.GetLookupTypeAsync(name, cancellationToken);
        var minimum = ReadAsync("FuturesOptionStrikePriceMin");
        var maximum = ReadAsync("FuturesOptionStrikePriceMax");
        var increment = ReadAsync("FuturesOptionStrikePriceIncrement");
        var values = await Task.WhenAll(minimum, maximum, increment).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        return new FuturesOptionStrikePriceReadModel
        {
            Minimum = Convert.ToInt32(values[0].FirstOrDefault()?.ShortCode, System.Globalization.CultureInfo.InvariantCulture),
            Maximum = Convert.ToInt32(values[1].FirstOrDefault()?.ShortCode, System.Globalization.CultureInfo.InvariantCulture),
            Increment = Convert.ToInt32(values[2].FirstOrDefault()?.ShortCode, System.Globalization.CultureInfo.InvariantCulture)
        };
    }
}
