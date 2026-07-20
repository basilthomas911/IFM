using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Commands;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.Queries;
using TomasAI.IFM.Shared.MarketDataFeed.QueryParameters;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;


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
    public static async ValueTask GenerateFuturesRsiSignalAsync(this IEventActorContext context, FuturesRsiSignalId futuresRsiSignalId, decimal futuresPrice)
    {
        var entityId = futuresRsiSignalId.ToEntityId();
        GenerateFuturesRsiSignalCommand cmd = new(futuresRsiSignalId, futuresPrice)
        {
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesRsiSignalCommand.Actor, GenerateFuturesRsiSignalCommand.Verb, entityId.Format()),
            FuturesRsiSignalId = futuresRsiSignalId,
            FuturesPrice = futuresPrice
        }; 
        var serviceResult = await context.RequestAsync<GenerateFuturesRsiSignalCommand, FuturesRsiSignalEntityId>(cmd);
        if (serviceResult?.Success != true)
            throw new InvalidOperationException(serviceResult?.ErrorMessage);
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
        this IEventActorContext context, FuturesTdiSignalId futuresTdiSignalId, FuturesRsiSignalReadModel[] futuresRsiSignals, TradeTimePeriodType timePeriod)
    {
        var entityId = new FuturesTdiSignalEntityId(futuresTdiSignalId.ContractId, futuresTdiSignalId.ValueDate, timePeriod);
        GenerateFuturesTdiSignalCommand cmd = new(futuresTdiSignalId, futuresRsiSignals)
        {
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesTdiSignalCommand.Actor, GenerateFuturesTdiSignalCommand.Verb, entityId.Format()),
            EntityId = entityId,
            ErrorCode = GenerateFuturesTdiSignalCommand.ErrorId
        };
        var serviceResult = await context.RequestAsync<GenerateFuturesTdiSignalCommand, FuturesTdiSignalEntityId>(cmd);
        if (serviceResult?.Success != true)
            throw new InvalidOperationException(serviceResult?.ErrorMessage);
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
        this IEventActorContext context, FuturesMacdSignalId futuresMacdSignalId, decimal futuresPrice)
    {
        var entityId = futuresMacdSignalId.ToEntityId();
        GenerateFuturesMacdSignalCommand cmd = new(futuresMacdSignalId, futuresPrice)
        {
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesMacdSignalCommand.Actor, GenerateFuturesMacdSignalCommand.Verb, entityId.Format()),
            EntityId = entityId,
            ErrorCode = GenerateFuturesMacdSignalCommand.ErrorId
        };
        var serviceResult = await context.RequestAsync<GenerateFuturesMacdSignalCommand, FuturesMacdSignalEntityId>(cmd);
        if (serviceResult?.Success != true)
            throw new InvalidOperationException(serviceResult?.ErrorMessage);
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
