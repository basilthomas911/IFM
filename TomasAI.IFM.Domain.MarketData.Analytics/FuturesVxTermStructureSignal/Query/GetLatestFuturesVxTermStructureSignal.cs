using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVxTermStructureSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Query.Extensions;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Query;

/// <summary>Handles latest projected VX term-structure queries.</summary>
public static class GetLatestFuturesVxTermStructureSignal
{
    /// <summary>Loads the latest projected curve and sends its typed Query reply.</summary>
    public static async ValueTask ExecuteAsync(
        this GetLatestFuturesVxTermStructureSignalQuery query,
        IQueryActorContext<FuturesVxTermStructureSignalQueryActor> context,
        CancellationToken cancellationToken)
    {
        var result = await context.DbFactory.MarketDataDb.GetLatestFuturesVxTermStructureSignalAsync(
            query.ValueDate, query.ConfigurationId, cancellationToken).ConfigureAwait(false);
        await context.ReplyAsync(query.Subject.ThreadId,
            GetLatestFuturesVxTermStructureSignalQuery.Verb,
            new ServiceResult<FuturesVxTermStructureSignalReadModel?>(result)).ConfigureAwait(false);
    }
}
