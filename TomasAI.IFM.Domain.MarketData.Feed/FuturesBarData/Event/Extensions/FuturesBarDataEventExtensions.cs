using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.MarketDataAnalytics.Queries;
using TomasAI.IFM.Shared.MarketDataAnalytics.QueryParameters;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.MarketDataFeed.Queries;
using TomasAI.IFM.Shared.MarketDataFeed.QueryParameters;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Event.Extensions;

internal static class FuturesBarDataEventExtensions
{
    internal static async ValueTask FuturesBarDataStreamingStartedCompleteAsync(
        this IEventActorContext context, FuturesBarDataStreamingStartedEvent e)
    {
        var completeEvent = e.ToCompleteEvent<FuturesBarDataStreamingStartedCompleteEvent, FuturesBarDataStreamingId>() as FuturesBarDataStreamingStartedCompleteEvent;
        await context.SendAsync<FuturesBarDataStreamingStartedCompleteEvent, FuturesBarDataStreamingId>(completeEvent!);
    }

    internal static async ValueTask FuturesBarDataStreamingStartedFailAsync(
        this IEventActorContext context, FuturesBarDataStreamingStartedEvent e, Exception ex)
    {
        var failEvent = e.ToFailEvent<FuturesBarDataStreamingStartedFailEvent, FuturesBarDataStreamingId>(ex) as FuturesBarDataStreamingStartedFailEvent;
        await context.SendAsync<FuturesBarDataStreamingStartedFailEvent, FuturesBarDataStreamingId>(failEvent!);
    }

    internal static async ValueTask FuturesBarDataStreamingStoppedCompleteAsync(
        this IEventActorContext context, FuturesBarDataStreamingStoppedEvent e)
    {
        var completeEvent = e.ToCompleteEvent<FuturesBarDataStreamingStoppedCompleteEvent, FuturesBarDataStreamingId>() as FuturesBarDataStreamingStoppedCompleteEvent;
        await context.SendAsync<FuturesBarDataStreamingStoppedCompleteEvent, FuturesBarDataStreamingId>(completeEvent!);
    }

    internal static async ValueTask FuturesBarDataStreamingStoppedFailAsync(
        this IEventActorContext context, FuturesBarDataStreamingStoppedEvent e, Exception ex)
    {
        var failEvent = e.ToFailEvent<FuturesBarDataStreamingStoppedFailEvent, FuturesBarDataStreamingId>(ex) as FuturesBarDataStreamingStoppedFailEvent;
        await context.SendAsync<FuturesBarDataStreamingStoppedFailEvent, FuturesBarDataStreamingId>(failEvent!);
    }

    /// <summary>
    /// Queries for the last futures tick data for the specified contract and value date.
    /// </summary>
    /// <param name="context">The event actor context used to dispatch the query.</param>
    /// <param name="contractId">The contract identifier to query tick data for.</param>
    /// <param name="valueDate">The value date to query tick data for.</param>
    /// <returns>The last futures tick data, or <see langword="null"/> if none is found.</returns>
    internal static async ValueTask<FuturesTickDataV2ReadModel?> GetLastFuturesTickDataAsync(
        this IEventActorContext context, string contractId, DateOnly valueDate)
    {
        var futuresTickData = default(FuturesTickDataV2ReadModel);
        var entityId = new GetLastFuturesTickDataParameter(contractId, valueDate);
        GetLastFuturesTickDataQuery query = new(contractId, valueDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetLastFuturesTickDataQuery.Actor, GetLastFuturesTickDataQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ErrorCode = GetLastFuturesTickDataQuery.ErrorId
        };
        var serviceResult = await context.RequestAsync<FuturesTickDataV2ReadModel, GetLastFuturesTickDataQuery>(query);
        if (serviceResult.Success && serviceResult.Value is not null)
            futuresTickData = serviceResult.Value;
        return futuresTickData;
    }

    /// <summary>
    /// Queries for the last futures trade signal for the specified symbol and value date.
    /// </summary>
    /// <param name="context">The event actor context used to dispatch the query.</param>
    /// <param name="symbol">The futures symbol to query the trade signal for.</param>
    /// <param name="valueDate">The value date to query the trade signal for.</param>
    /// <returns>The last futures trade signal, or <see langword="null"/> if none is found.</returns>
    internal static async ValueTask<FuturesTradeSignalV2ReadModel?> GetLastFuturesTradeSignalAsync(
        this IEventActorContext context, string symbol, DateOnly valueDate)
    {
        var futuresTradeSignal = default(FuturesTradeSignalV2ReadModel);
        var entityId = new GetFuturesTradeSignalBySymbolParameter(symbol, valueDate);
        GetFuturesTradeSignalBySymbolQuery query = new(symbol, valueDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesTradeSignalBySymbolQuery.Actor, GetFuturesTradeSignalBySymbolQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ErrorCode = GetLastFuturesTickDataQuery.ErrorId
        };
        var serviceResult = await context.RequestAsync<FuturesTradeSignalV2ReadModel, GetFuturesTradeSignalBySymbolQuery>(query);
        if (serviceResult.Success && serviceResult.Value is not null)
            futuresTradeSignal = serviceResult.Value;
        return futuresTradeSignal;
    }

    /// <summary>
    /// Sends a command to insert a futures bar data record.
    /// </summary>
    /// <param name="context">The event actor context used to dispatch the command.</param>
    /// <param name="futuresBarData">The futures bar data view model to insert.</param>
    /// <exception cref="InvalidOperationException">Thrown when the command request fails.</exception>
    internal static async ValueTask InsertFuturesBarDataAsync(
        this IEventActorContext context, FuturesBarDataReadModel futuresBarData)
    {
        InsertFuturesBarDataCommand cmd = new(futuresBarData)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesBarDataCommand.Actor, InsertFuturesBarDataCommand.Verb, futuresBarData.Id.Format()),
            EntityId = futuresBarData.Id,
            ErrorCode = InsertFuturesBarDataCommand.ErrorId
        };
        var serviceResult = await context.RequestAsync<InsertFuturesBarDataCommand, FuturesBarDataId>(cmd);
        if (serviceResult?.Success != true)
            throw new InvalidOperationException(serviceResult?.ErrorMessage);
    }

}
    