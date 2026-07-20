using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Fund.Shared.Queries;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Query;

internal static class GetFunds
{
    /// <summary>
    /// Handles the GetFundsQuery by retrieving all funds from the database and replying with the results.
    /// </summary>
    /// <param name="q">The GetFundsQuery instance.</param>
    /// <param name="dbFactory">The database context factory.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    internal static async ValueTask<FundReadModel[]> GetFundsAsync(this GetFundsQuery q, IDbContextFactory dbFactory)
        => [.. await dbFactory.FundDb.GetFundsAsync()];
}
