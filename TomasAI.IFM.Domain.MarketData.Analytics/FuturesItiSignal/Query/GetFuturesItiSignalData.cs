using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.Queries;
using TomasAI.IFM.Shared.MarketDataAnalytics.Queries;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Query;

public static class GetFuturesItiSignalData
{
    /// <summary>
    /// Handles the GetFuturesItiSignalDataQuery by retrieving the last Futures ITI signal data for a given contract and value date from the database, and replies with the result.
    /// </summary>
    /// <param name="q">The query for retrieving Futures ITI signal data.</param>
    /// <param name="dbFactory">The factory for creating database contexts.</param>
    /// <param name="context">The query actor context for replying to the query.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    internal static async ValueTask GetFuturesItiSignalDataAsync(
        this GetFuturesItiSignalDataQuery q, IDbContextFactory dbFactory, IQueryActorContext context)
    {
        var db = dbFactory.MarketDataDb;
        var result = new FuturesItiSignalDataReadModel(
            trendDirectionChange: await db.GetLastFuturesItiSignalTrendDirectionChangeAsync(q.ContractId, q.ValueDate),
            trendExtremeChange: await db.GetLastFuturesItiSignalTrendExtremeChangeAsync(q.ContractId, q.ValueDate),
            trendReversalChange: await db.GetLastFuturesItiSignalTrendReversalChangeAsync(q.ContractId, q.ValueDate));
        await context.ReplyAsync(q.Subject.ThreadId, GetFuturesItiSignalDataQuery.Verb, new ServiceResult<FuturesItiSignalDataReadModel?>(result));
    }
}
