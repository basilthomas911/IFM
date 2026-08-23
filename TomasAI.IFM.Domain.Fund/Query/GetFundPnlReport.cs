using TomasAI.IFM.Domain.Fund.Query.Actor;
using TomasAI.IFM.Domain.Fund.Shared.Queries;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Fund.Query;

internal static class GetFundPnlReport
{
    /// <summary>
    /// handle GetFundPnlReportQuery, calculate fund pnl report based on fund transactions, and reply with FundPnlReportReadModel
    /// </summary>
    /// <param name="q"> The query to handle </param>
    /// <param name="context">The Fund-specific query context.</param>
    /// <param name="cancellationToken">The token used to cancel the query.</param>
    /// <returns> A task representing the asynchronous operation </returns>
    internal static async ValueTask<FundPnlReportReadModel> GetFundPnlReportAsync(
        this GetFundPnlReportQuery q,
        IFundQueryContext context,
        CancellationToken cancellationToken = default)
        => await FundQueryCalculations.GetPnlReportAsync(
            context.DbFactory.FundDb,
            q.FundId,
            q.StartDate,
            q.EndDate,
            cancellationToken).ConfigureAwait(false);
}
