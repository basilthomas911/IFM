using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Commands;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

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
    public static async ValueTask GenerateFuturesAdxSignalAsync(this IEventActorContext context, FuturesEodDataV2ReadModel futuresEodData,TradeTimePeriodType timePeriod, int periodLength, decimal futuresPrice)
    {
        var signalId = new FuturesAdxSignalId(futuresEodData.ContractId, futuresEodData.ValueDate, timePeriod, periodLength, TimeOnly.FromDateTime(DateTime.Now));
        var entityId = signalId.ToEntityId();
        GenerateFuturesAdxSignalCommand cmd = new(signalId, futuresPrice)
        {
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesAdxSignalCommand.Actor, GenerateFuturesAdxSignalCommand.Verb, entityId.Format()),
        };
        var serviceResult = await context.RequestAsync<GenerateFuturesAdxSignalCommand, FuturesAdxSignalEntityId>(cmd);
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
        };
        var serviceResult = await context.RequestAsync<GenerateFuturesMacdSignalCommand, FuturesMacdSignalEntityId>(cmd);
        if (serviceResult?.Success != true)
            throw new InvalidOperationException(serviceResult?.ErrorMessage);
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
        this IEventActorContext context, FuturesAtrSignalId futuresAtrSignalId, decimal futuresPrice)
    {
        var entityId = futuresAtrSignalId.ToEntityId();
        GenerateFuturesAtrSignalCommand cmd = new(futuresAtrSignalId, futuresPrice)
        {
            Subject = new ActorSubject(ActorType.Command, GenerateFuturesAtrSignalCommand.Actor, GenerateFuturesAtrSignalCommand.Verb, entityId.Format()),
        };
        var serviceResult = await context.RequestAsync<GenerateFuturesAtrSignalCommand, FuturesAtrSignalEntityId>(cmd);
        if (serviceResult?.Success != true)
            throw new InvalidOperationException(serviceResult?.ErrorMessage);
    }

}
