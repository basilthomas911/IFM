using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Fund.Shared.Queries;

namespace TomasAI.IFM.Domain.Fund.Query;

internal static class GetFundIdFromOrderId
{
    /// <summary>
    /// Handles the GetFundIdFromOrderIdQuery by querying the fund_order table in the FundDb to retrieve the associated FundId for the given OrderId. 
    /// The result is then sent back as a reply to the query actor context.
    /// </summary>
    /// <param name="q">The query for retrieving the fund ID.</param>
    /// <param name="dbFactory">The database context factory.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    internal static async ValueTask<int> GetFundIdFromOrderIdAsync(
        this GetFundIdFromOrderIdQuery q, IDbContextFactory dbFactory)
        => await dbFactory.FundDb.GetFundIdFromOrderIdAsync(q.OrderId);

}
