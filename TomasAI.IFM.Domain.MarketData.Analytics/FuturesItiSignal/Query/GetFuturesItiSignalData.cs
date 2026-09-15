using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Query.Actor;
using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

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
    internal static async ValueTask<FuturesItiSignalDataReadModel> GetFuturesItiSignalDataAsync(
        this GetFuturesItiSignalDataQuery q,
        IDbContextFactory dbFactory,
        CancellationToken cancellationToken = default)
    {
        var db = dbFactory.MarketDataDb;
        var trendDirectionTask = cancellationToken.CanBeCanceled
            ? db.GetLastFuturesItiSignalTrendDirectionChangeAsync(
                q.ContractId, q.ValueDate, cancellationToken)
            : db.GetLastFuturesItiSignalTrendDirectionChangeAsync(q.ContractId, q.ValueDate);
        var trendExtremeTask = cancellationToken.CanBeCanceled
            ? db.GetLastFuturesItiSignalTrendExtremeChangeAsync(
                q.ContractId, q.ValueDate, cancellationToken)
            : db.GetLastFuturesItiSignalTrendExtremeChangeAsync(q.ContractId, q.ValueDate);
        var trendReversalTask = cancellationToken.CanBeCanceled
            ? db.GetLastFuturesItiSignalTrendReversalChangeAsync(
                q.ContractId, q.ValueDate, cancellationToken)
            : db.GetLastFuturesItiSignalTrendReversalChangeAsync(q.ContractId, q.ValueDate);
        await Task.WhenAll(trendDirectionTask, trendExtremeTask, trendReversalTask)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return new FuturesItiSignalDataReadModel(
            trendDirectionChange: await trendDirectionTask.ConfigureAwait(false),
            trendExtremeChange: await trendExtremeTask.ConfigureAwait(false),
            trendReversalChange: await trendReversalTask.ConfigureAwait(false));
    }

    /// <summary>Reads and replies to the GetFuturesItiSignalDataQuery message.</summary>
    public static async ValueTask ExecuteAsync(
        this GetFuturesItiSignalDataQuery q,
        IQueryActorContext<FuturesItiSignalQueryActor> ctx,
        IDbContextFactory db,
        CancellationToken cancellationToken)
    {
        var query = (q as GetFuturesItiSignalDataQuery)!;
        var result = await query.GetFuturesItiSignalDataAsync(db, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        await ctx.ReplyAsync(q.Subject.ThreadId, GetFuturesItiSignalDataQuery.Verb,
            new ServiceResult<FuturesItiSignalDataReadModel>(result)).ConfigureAwait(false);
    }
}
