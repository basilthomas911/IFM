using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Reference.Shared.Queries;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Reference.Query;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S101", Justification = "The published query handler name preserves the domain acronym and existing query-family convention.")]
public static class GetMDIForwardLossRatios
{
    /// <summary>
    /// Handles a request to retrieve MDI forward loss ratios for the trend direction and trade type carried by the query.
    /// </summary>
    public static async ValueTask<MDIForwardLossRatioReadModel[]> ExecuteAsync(
        this GetMDIForwardLossRatiosQuery q, IDbContextFactory dbFactory, CancellationToken cancellationToken = default)
        
    {
        ArgumentNullException.ThrowIfNull(dbFactory);
        ArgumentNullException.ThrowIfNull(q);
        return [.. await (dbFactory.ReferenceDb.GetMDIForwardLossRatiosAsync(q.TrendDirection, q.TradeType, cancellationToken)).ConfigureAwait(false)];
    }

    /// <summary>Reads and replies with the requested Reference result.</summary>
    public static async ValueTask ExecuteAsync(this GetMDIForwardLossRatiosQuery query, Actor.IReferenceQueryContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(query);
        var result = await query.ExecuteAsync(context.DbFactory, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, new ServiceResult<MDIForwardLossRatioReadModel[]>(result)).ConfigureAwait(false);
    }
}
