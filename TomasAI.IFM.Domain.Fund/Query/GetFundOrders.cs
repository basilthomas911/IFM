using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Fund.Shared.Queries;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Query;

internal static class GetFundOrders
{
    /// <summary>
    /// Handles the GetFundOrdersQuery by retrieving fund orders from the database and replying with the results.
    /// </summary>
    /// <param name="q">The query to handle.</param>
    /// <param name="dbFactory">The database context factory.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    internal static async ValueTask<FundOrderReadModel[]> GetFundOrdersAsync(
        this GetFundOrdersQuery q, IDbContextFactory dbFactory)
        => [.. await dbFactory.FundDb.GetFundOrdersAsync()];
}
