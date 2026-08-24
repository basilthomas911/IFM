using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Event.Extensions;

internal static class FuturesAdxSignalEventExtensions
{
    /// <summary>
    /// Generates a futures RSI signal by sending a command to the actor system using the provided end-of-day data.
    /// </summary>
    /// <param name="context">The event actor context used to dispatch the command.</param>
    /// <param name="futuresEodData">The end-of-day futures data used as input for RSI signal generation.</param>
    /// <returns>A ValueTask representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the generate operation fails or returns an unsuccessful result.</exception>
    public static async ValueTask GenerateFuturesAdxSignalAsync(this IEventActorContext commandApi, FuturesEodDataV2ReadModel futuresEodData,TimeFrameType timePeriod, int periodLength, decimal futuresPrice)
    {
        var signalId = new FuturesAdxSignalId(futuresEodData.ContractId, futuresEodData.ValueDate, timePeriod, periodLength, TimeOnly.FromDateTime(DateTime.Now));
        _ = await MarketDataAnalyticsCommandApiExtensions.GenerateFuturesAdxSignalAsync(commandApi, signalId, futuresPrice);
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
        this IEventActorContext commandApi, FuturesTdiSignalId futuresTdiSignalId, FuturesRsiSignalReadModel[] futuresRsiSignals, TimeFrameType timePeriod)
    {
        _ = await MarketDataAnalyticsCommandApiExtensions.GenerateFuturesTdiSignalAsync(commandApi, futuresTdiSignalId, futuresRsiSignals, timePeriod);
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
    /// Generates a futures ATR signal by sending a command to the actor system using the provided RSI signals.
    /// </summary>
    /// <param name="context">The event actor context used to dispatch the command.</param>
    /// <param name="futuresAtrSignalId">Target ATR signal identifier (contract + value date + timestamp context).</param>
    /// <param name="futuresPrice">Input RSI signal series used to compute the ATR signal.</param>
    /// <returns>A ValueTask representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the generate operation fails or returns an unsuccessful result.</exception>
    public static async ValueTask GenerateFuturesAtrSignalAsync(
        this IEventActorContext commandApi, FuturesAtrSignalId futuresAtrSignalId, decimal futuresPrice)
    {
        _ = await MarketDataAnalyticsCommandApiExtensions.GenerateFuturesAtrSignalAsync(commandApi, futuresAtrSignalId, futuresPrice);
    }

}
