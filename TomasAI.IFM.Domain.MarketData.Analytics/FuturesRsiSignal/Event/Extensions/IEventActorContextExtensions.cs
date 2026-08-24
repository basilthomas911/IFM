using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;


namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event.Extensions;

internal static class IEventActorContextExtensions
{
    /// <summary>
    /// Generates a futures RSI signal by sending a command to the actor system using the provided end-of-day data.
    /// </summary>
    /// <param name="context">The event actor context used to dispatch the command.</param>
    /// <param name="futuresEodData">The end-of-day futures data used as input for RSI signal generation.</param>
    /// <param name="signalType">The type of RSI signal to generate (e.g., daily, weekly).</param>
    /// <param name="periodLength">The period length for the RSI calculation.</param>
    /// <returns>A ValueTask representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the generate operation fails or returns an unsuccessful result.</exception>
    public static async ValueTask GenerateFuturesRsiSignalAsync(
        this IEventActorContext commandApi,
        FuturesRsiSignalId futuresRsiSignalId,
        decimal futuresPrice,
        long sourceSequence = 0,
        DateTime sourceEventTimestamp = default)
    {
        _ = await MarketDataAnalyticsCommandApiExtensions.GenerateFuturesRsiSignalAsync(commandApi,
            futuresRsiSignalId,
            futuresPrice,
            sourceSequence,
            sourceEventTimestamp);
    }

    /// <summary>
    /// Generates a futures TDI signal by sending a command to the actor system using the provided RSI signals.
    /// </summary>
    /// <param name="context">The event actor context used to dispatch the command.</param>
    /// <param name="futuresTdiSignalId">Target TDI signal identifier (contract + value date + timestamp context).</param>
    /// <param name="futuresRsiSignals">Input RSI signal series used to compute the TDI signal.</param>
    /// <returns>A ValueTask representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the generate operation fails or returns an unsuccessful result.</exception>
    public static async ValueTask GenerateFuturesTdiSignalAsync(
        this IEventActorContext commandApi,
        FuturesTdiSignalId futuresTdiSignalId,
        FuturesRsiSignalReadModel[] futuresRsiSignals,
        TimeFrameType timePeriod,
        FuturesTdiConfiguration? configuration = null,
        Guid? commandId = null)
    {
        _ = await MarketDataAnalyticsCommandApiExtensions.GenerateFuturesTdiSignalAsync(commandApi,
            futuresTdiSignalId,
            futuresRsiSignals,
            timePeriod,
            configuration,
            commandId);
    }

    /// <summary>
    /// Generates a futures MACD signal by sending a command to the actor system using the provided RSI signals.
    /// </summary>
    /// <param name="context">The event actor context used to dispatch the command.</param>
    /// <param name="futuresMacdSignalId">Target MACD signal identifier (contract + value date + timestamp context).</param>
    /// <param name="futuresRsiSignals">Input RSI signal series used to compute the MACD signal.</param>
    /// <param name="timePeriod">The time period type for the MACD signal.</param>
    /// <returns>A ValueTask representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the generate operation fails or returns an unsuccessful result.</exception>
    public static async ValueTask GenerateFuturesMacdSignalAsync(
        this IEventActorContext commandApi, FuturesMacdSignalId futuresMacdSignalId, decimal futuresPrice)
    {
        _ = await MarketDataAnalyticsCommandApiExtensions.GenerateFuturesMacdSignalAsync(commandApi, futuresMacdSignalId, futuresPrice);
    }


    /// <summary>
    /// Retrieves the most recent end-of-day (EOD) futures data for a specified contract and value date.
    /// </summary>
    /// <remarks>This method performs an asynchronous request and may return null if the operation is
    /// unsuccessful or if no data is available for the specified parameters.</remarks>
    /// <param name="contractId">The unique identifier of the futures contract for which EOD data is requested.</param>
    /// <param name="valueDate">The date for which the most recent end-of-day data is retrieved.</param>
    /// <returns>A task representing the asynchronous operation. The result contains the EOD data view model,
    /// or null if no data is found.</returns>
    public static async ValueTask<FuturesEodDataV2ReadModel?> GetLastFuturesEodDataAsync(this IEventActorContext context, string contractId, DateOnly valueDate)
    {
        var futuresEodData = default(FuturesEodDataV2ReadModel);
        var entityId = new GetLastFuturesEodDataParameter(contractId, valueDate);
        GetLastFuturesEodDataQuery query = new(contractId, valueDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetLastFuturesEodDataQuery.Actor, GetLastFuturesEodDataQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ErrorCode = GetLastFuturesEodDataQuery.ErrorId
        };
        var serviceResult = await context.RequestAsync<FuturesEodDataV2ReadModel, GetLastFuturesEodDataQuery>(query);
        if (serviceResult.Success && serviceResult.Value is not null)
            futuresEodData = serviceResult.Value;
        return futuresEodData;
    }
}
